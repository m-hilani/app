using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompressorApp
{
    // Thread-safe progress reporter
    public class ThreadSafeProgressReporter
    {
        private readonly Action<int> progressCallback;
        private readonly object lockObject = new object();
        private int currentProgress = 0;

        public ThreadSafeProgressReporter(Action<int> progressCallback)
        {
            this.progressCallback = progressCallback;
        }

        public void ReportProgress(int progress)
        {
            lock (lockObject)
            {
                if (progress > currentProgress)
                {
                    currentProgress = progress;
                    progressCallback?.Invoke(progress);
                }
            }
        }
    }

    // Compressed chunk data for parallel processing
    public class CompressedChunk
    {
        public int Index { get; set; }
        public byte[] Data { get; set; }
        public int OriginalStartIndex { get; set; }
        public int OriginalLength { get; set; }
    }

    // Configuration for multi-threading
    public static class CompressionConfig
    {
        public static int MaxThreads { get; set; } = Environment.ProcessorCount;
        public static int ChunkSizeBytes { get; set; } = 1024 * 1024; // 1MB chunks
        public static int MinFileSizeForChunking { get; set; } = 5 * 1024 * 1024; // 5MB minimum
    }

    public interface ICompressionAlgorithm
    {
        Task CompressAsync(string inputPath, string outputPath, string password = null,
                         Action<int> progressCallback = null, CancellationToken cancellationToken = default);
        Task DecompressAsync(string inputPath, string outputPath, string password = null,
                           Action<int> progressCallback = null, CancellationToken cancellationToken = default);
        double GetCompressionRatio();
        Task<double> EstimateCompressionRatioAsync(string inputPath);

        Task CompressMultipleAsync(Dictionary<string, string> filePaths, string outputPath, string password = null,
                             Action<int> progressCallback = null, CancellationToken cancellationToken = default);
        Task<string> ExtractSingleFileAsync(string archivePath, string fileToExtract, string outputPath, string password = null,
                                          Action<int> progressCallback = null, CancellationToken cancellationToken = default);
        Task<List<string>> ListFilesInArchiveAsync(string archivePath, string password = null);
    }

    // Helper class for encryption/decryption
    public static class PasswordProtection
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32; // 256 bits
        private const int Iterations = 10000;

        public static byte[] Encrypt(byte[] data, string password)
        {
            if (string.IsNullOrEmpty(password))
                return data;

            // Generate random salt and IV
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            // Derive key from password
            byte[] key = DeriveKey(password, salt);

            // Encrypt data
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    // Write salt and IV first
                    msEncrypt.Write(salt, 0, salt.Length);
                    msEncrypt.Write(iv, 0, iv.Length);

                    // Encrypt data
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                    }

                    return msEncrypt.ToArray();
                }
            }
        }

        public static byte[] Decrypt(byte[] encryptedData, string password)
        {
            if (string.IsNullOrEmpty(password))
                return encryptedData;

            if (encryptedData.Length < SaltSize + IvSize)
                throw new InvalidDataException("البيانات المشفرة غير صالحة");

            // Extract salt and IV
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            Array.Copy(encryptedData, 0, salt, 0, SaltSize);
            Array.Copy(encryptedData, SaltSize, iv, 0, IvSize);

            // Derive key from password
            byte[] key = DeriveKey(password, salt);

            // Decrypt data
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                try
                {
                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(encryptedData, SaltSize + IvSize, encryptedData.Length - SaltSize - IvSize))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var msPlain = new MemoryStream())
                    {
                        csDecrypt.CopyTo(msPlain);
                        return msPlain.ToArray();
                    }
                }
                catch (CryptographicException)
                {
                    throw new UnauthorizedAccessException("كلمة السر غير صحيحة");
                }
            }
        }

        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                return pbkdf2.GetBytes(KeySize);
            }
        }

        public static bool IsPasswordProtected(byte[] data)
        {
            // Check if data length suggests it might be encrypted
            return data.Length > SaltSize + IvSize;
        }
    }

    public class HuffmanCompressor : ICompressionAlgorithm
    {
        private class HuffmanNode : IComparable<HuffmanNode>
        {
            public byte Symbol { get; set; }
            public int Frequency { get; set; }
            public HuffmanNode Left { get; set; }
            public HuffmanNode Right { get; set; }
            public bool IsLeaf => Left == null && Right == null;
            public int CompareTo(HuffmanNode other) => Frequency.CompareTo(other.Frequency);
        }

        private double compressionRatio;
        private bool isPaused;
        private object pauseLock = new object();

        public double GetCompressionRatio() => compressionRatio;

        public void Pause() => isPaused = true;
        public void Resume() { isPaused = false; lock (pauseLock) { Monitor.Pulse(pauseLock); } }

        // Parallel compression helper method
        private byte[] CompressDataParallel(byte[] data, Dictionary<byte, string> codes, 
                                           ThreadSafeProgressReporter progressReporter, 
                                           CancellationToken cancellationToken)
        {
            if (data.Length < CompressionConfig.MinFileSizeForChunking)
            {
                // For small files, use sequential compression
                return CompressDataSequential(data, codes, progressReporter, cancellationToken);
            }

            // Split data into chunks for parallel processing
            int chunkSize = CompressionConfig.ChunkSizeBytes;
            var chunks = new List<ArraySegment<byte>>();
            
            for (int i = 0; i < data.Length; i += chunkSize)
            {
                int currentChunkSize = Math.Min(chunkSize, data.Length - i);
                chunks.Add(new ArraySegment<byte>(data, i, currentChunkSize));
            }

            // Compress chunks in parallel
            var compressedChunks = new ConcurrentBag<CompressedChunk>();
            var processedChunks = 0;
            var totalChunks = chunks.Count;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = CompressionConfig.MaxThreads,
                CancellationToken = cancellationToken
            };

            Parallel.ForEach(chunks.Select((chunk, index) => new { chunk, index }), parallelOptions, item =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                CheckPauseState(cancellationToken);

                var chunkData = item.chunk.ToArray();
                var compressedData = CompressChunk(chunkData, codes, cancellationToken);
                
                compressedChunks.Add(new CompressedChunk
                {
                    Index = item.index,
                    Data = compressedData,
                    OriginalStartIndex = item.chunk.Offset,
                    OriginalLength = item.chunk.Count
                });

                var completed = Interlocked.Increment(ref processedChunks);
                progressReporter?.ReportProgress((int)((completed * 100.0) / totalChunks));
            });

            // Assemble compressed chunks in order
            var orderedChunks = compressedChunks.OrderBy(c => c.Index).ToList();
            using (var finalStream = new MemoryStream())
            {
                foreach (var chunk in orderedChunks)
                {
                    finalStream.Write(chunk.Data, 0, chunk.Data.Length);
                }
                return finalStream.ToArray();
            }
        }

        // Sequential compression for small files or fallback
        private byte[] CompressDataSequential(byte[] data, Dictionary<byte, string> codes, 
                                             ThreadSafeProgressReporter progressReporter, 
                                             CancellationToken cancellationToken)
        {
            using (var compressedDataStream = new MemoryStream())
            using (var tempWriter = new BinaryWriter(compressedDataStream))
            {
                byte buffer = 0;
                int bufferLength = 0;
                int totalBytes = data.Length;

                for (int i = 0; i < totalBytes; i++)
                {
                    CheckPauseState(cancellationToken);
                    string code = codes[data[i]];
                    foreach (char bit in code)
                    {
                        buffer = (byte)((buffer << 1) | (bit == '1' ? 1 : 0));
                        bufferLength++;

                        if (bufferLength == 8)
                        {
                            tempWriter.Write(buffer);
                            buffer = 0;
                            bufferLength = 0;
                        }
                    }

                    if (i % 1000 == 0 || i == totalBytes - 1)
                    {
                        int progress = (int)((i * 100.0) / totalBytes);
                        progressReporter?.ReportProgress(progress);
                    }
                }

                if (bufferLength > 0)
                {
                    buffer <<= (8 - bufferLength);
                    tempWriter.Write(buffer);
                }

                return compressedDataStream.ToArray();
            }
        }

        // Compress a single chunk
        private byte[] CompressChunk(byte[] chunkData, Dictionary<byte, string> codes, CancellationToken cancellationToken)
        {
            using (var compressedStream = new MemoryStream())
            using (var writer = new BinaryWriter(compressedStream))
            {
                byte buffer = 0;
                int bufferLength = 0;

                foreach (byte b in chunkData)
                {
                    CheckPauseState(cancellationToken);
                    string code = codes[b];
                    foreach (char bit in code)
                    {
                        buffer = (byte)((buffer << 1) | (bit == '1' ? 1 : 0));
                        bufferLength++;

                        if (bufferLength == 8)
                        {
                            writer.Write(buffer);
                            buffer = 0;
                            bufferLength = 0;
                        }
                    }
                }

                if (bufferLength > 0)
                {
                    buffer <<= (8 - bufferLength);
                    writer.Write(buffer);
                }

                return compressedStream.ToArray();
            }
        }

        // Parallel file processing helper
        private class FileCompressionTask
        {
            public string FilePath { get; set; }
            public string RelativePath { get; set; }
            public byte[] Data { get; set; }
            public Dictionary<byte, int> Frequencies { get; set; }
            public Dictionary<byte, string> Codes { get; set; }
            public byte[] CompressedData { get; set; }
            public int Index { get; set; }
        }

        public async Task CompressMultipleAsync(Dictionary<string, string> filePaths, string outputPath, string password = null,
                                         Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var progressReporter = new ThreadSafeProgressReporter(progressCallback);
                var totalFiles = filePaths.Count;
                
                // Phase 1: Parallel file processing (read, analyze, compress)
                var fileTasks = new List<FileCompressionTask>();
                var fileArray = filePaths.ToArray();
                
                for (int i = 0; i < fileArray.Length; i++)
                {
                    var file = fileArray[i];
                    fileTasks.Add(new FileCompressionTask
                    {
                        FilePath = file.Key,
                        RelativePath = file.Value,
                        Index = i
                    });
                }

                // Process files in parallel
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = CompressionConfig.MaxThreads,
                    CancellationToken = cancellationToken
                };

                var processedFiles = 0;
                Parallel.ForEach(fileTasks, parallelOptions, task =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CheckPauseState(cancellationToken);

                    // Read and analyze file
                    task.Data = File.ReadAllBytes(task.FilePath);
                    task.Frequencies = CalculateFrequencies(task.Data);
                    var root = BuildHuffmanTree(task.Frequencies);
                    task.Codes = BuildCodeTable(root);

                    // Compress file data
                    var fileProgressReporter = new ThreadSafeProgressReporter(null); // No individual progress for parallel files
                    task.CompressedData = CompressDataParallel(task.Data, task.Codes, fileProgressReporter, cancellationToken);

                    // Update overall progress
                    var completed = Interlocked.Increment(ref processedFiles);
                    progressReporter.ReportProgress((int)((completed * 80.0) / totalFiles)); // Use 80% for processing
                });

                // Phase 2: Sequential file writing (to maintain archive format)
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("HUFFMAN_MULTI"); // توقيع خاص بالملفات المتعددة
                    writer.Write(!string.IsNullOrEmpty(password)); // هل الملف محمي بكلمة سر؟
                    writer.Write(filePaths.Count); // عدد الملفات

                    // Write files in order
                    var orderedTasks = fileTasks.OrderBy(t => t.Index).ToList();
                    for (int i = 0; i < orderedTasks.Count; i++)
                    {
                        var task = orderedTasks[i];
                        
                        writer.Write(Path.GetFileName(task.FilePath)); // اسم الملف
                        writer.Write(task.Data.Length); // حجم الملف الأصلي
                        writer.Write(task.Frequencies.Count); // عدد الترددات

                        foreach (var pair in task.Frequencies)
                        {
                            writer.Write(pair.Key);
                            writer.Write(pair.Value);
                        }

                        // Encrypt compressed data if password provided
                        byte[] compressedData = task.CompressedData;
                        if (!string.IsNullOrEmpty(password))
                        {
                            compressedData = PasswordProtection.Encrypt(compressedData, password);
                        }

                        writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                        writer.Write(compressedData); // البيانات المضغوطة (مشفرة إذا كانت محمية)

                        // Update progress for writing phase (80% + 20% for writing)
                        int writeProgress = 80 + (int)((i + 1) * 20.0 / totalFiles);
                        progressReporter.ReportProgress(writeProgress);
                    }
                }
                
                progressReporter.ReportProgress(100);
            }, cancellationToken);
        }

        public async Task<string> ExtractSingleFileAsync(string archivePath, string fileToExtract, string outputPath, string password = null,
                                                       Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using (var fs = new FileStream(archivePath, FileMode.Open))
                using (var reader = new BinaryReader(fs))
                {
                    if (reader.ReadString() != "HUFFMAN_MULTI")
                        throw new InvalidDataException("هذا ليس ملف مضغوط متعدد");

                    bool isPasswordProtected = reader.ReadBoolean();
                    if (isPasswordProtected && string.IsNullOrEmpty(password))
                        throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                    int fileCount = reader.ReadInt32();
                    string foundFileName = null;

                    for (int i = 0; i < fileCount; i++)
                    {
                        string fileName = reader.ReadString();
                        int originalSize = reader.ReadInt32();
                        int freqCount = reader.ReadInt32();
                        var frequencies = new Dictionary<byte, int>();

                        for (int j = 0; j < freqCount; j++)
                            frequencies[reader.ReadByte()] = reader.ReadInt32();

                        int compressedDataSize = reader.ReadInt32();
                        byte[] compressedData = reader.ReadBytes(compressedDataSize);

                        // Update progress for file search
                        progressCallback?.Invoke((int)((i * 50.0) / fileCount));

                        // Decrypt if password protected
                        if (isPasswordProtected)
                        {
                            compressedData = PasswordProtection.Decrypt(compressedData, password);
                        }

                        if (fileName.Equals(fileToExtract, StringComparison.OrdinalIgnoreCase))
                        {
                            var root = BuildHuffmanTree(frequencies);
                            var decompressedData = new byte[originalSize];
                            var currentNode = root;
                            int bitIndex = 0;
                            byte currentByte = 0;
                            int dataIndex = 0;
                            int byteIndex = 0;

                            while (dataIndex < originalSize && byteIndex < compressedData.Length)
                            {
                                CheckPauseState(cancellationToken);

                                if (bitIndex == 0)
                                {
                                    currentByte = compressedData[byteIndex++];
                                    bitIndex = 8;
                                }

                                int bit = (currentByte >> 7) & 1;
                                currentByte <<= 1;
                                bitIndex--;

                                currentNode = (bit == 0) ? currentNode.Left : currentNode.Right;

                                if (currentNode.IsLeaf)
                                {
                                    decompressedData[dataIndex++] = currentNode.Symbol;
                                    currentNode = root;
                                }

                                // Update progress for decompression
                                if (dataIndex % 1000 == 0 || dataIndex == originalSize)
                                {
                                    int decompressProgress = (int)((dataIndex * 50.0) / originalSize);
                                    progressCallback?.Invoke(50 + decompressProgress);
                                }
                            }

                            File.WriteAllBytes(outputPath, decompressedData);
                            foundFileName = fileName;
                            progressCallback?.Invoke(100);
                            break;
                        }
                    }

                    return foundFileName ?? throw new FileNotFoundException("الملف المطلوب غير موجود في الأرشيف");
                }
            }, cancellationToken);
        }

        public async Task<List<string>> ListFilesInArchiveAsync(string archivePath, string password = null)
        {
            return await Task.Run(() =>
            {
                var fileList = new List<string>();
                using (var fs = new FileStream(archivePath, FileMode.Open))
                using (var reader = new BinaryReader(fs))
                {
                    if (reader.ReadString() != "HUFFMAN_MULTI")
                        throw new InvalidDataException("هذا ليس ملف مضغوط متعدد");

                    bool isPasswordProtected = reader.ReadBoolean();
                    if (isPasswordProtected && string.IsNullOrEmpty(password))
                        throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                    int fileCount = reader.ReadInt32();

                    for (int i = 0; i < fileCount; i++)
                    {
                        string fileName = reader.ReadString();
                        fileList.Add(fileName);
                        
                        // Skip the file data without reading it
                        int originalSize = reader.ReadInt32();
                        int freqCount = reader.ReadInt32();
                        
                        // Skip frequency table
                        for (int j = 0; j < freqCount; j++)
                        {
                            reader.ReadByte(); // symbol
                            reader.ReadInt32(); // frequency
                        }
                        
                        // Skip compressed data using the exact size
                        int compressedDataSize = reader.ReadInt32();
                        reader.ReadBytes(compressedDataSize);
                    }
                }
                return fileList;
            });
        }

        public async Task<double> EstimateCompressionRatioAsync(string inputPath)
        {
            return await Task.Run(() =>
            {
                byte[] sampleData = File.ReadAllBytes(inputPath).Take(1024 * 1024).ToArray(); // عينة 1MB
                var frequencies = CalculateFrequencies(sampleData);
                var root = BuildHuffmanTree(frequencies);
                var codes = BuildCodeTable(root);

                double originalBits = sampleData.Length * 8;
                double compressedBits = sampleData.Sum(b => codes[b].Length);
                return (originalBits - compressedBits) / originalBits * 100;
            });
        }

        public async Task CompressAsync(string inputPath, string outputPath, string password = null,
                                      Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var progressReporter = new ThreadSafeProgressReporter(progressCallback);
                
                // Phase 1: Read and analyze file (10% of progress)
                byte[] data = File.ReadAllBytes(inputPath);
                progressReporter.ReportProgress(10);
                
                var frequencies = CalculateFrequencies(data);
                var root = BuildHuffmanTree(frequencies);
                var codes = BuildCodeTable(root);
                progressReporter.ReportProgress(20);

                // Phase 2: Compress data using parallel processing (60% of progress)
                var compressionProgressReporter = new ThreadSafeProgressReporter(progress => 
                {
                    int adjustedProgress = 20 + (int)(progress * 0.6); // Map 0-100 to 20-80
                    progressReporter.ReportProgress(adjustedProgress);
                });
                
                byte[] compressedData = CompressDataParallel(data, codes, compressionProgressReporter, cancellationToken);
                progressReporter.ReportProgress(80);

                // Phase 3: Write to file (20% of progress)
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("HUFFMAN"); // التوقيع
                    writer.Write(!string.IsNullOrEmpty(password)); // هل الملف محمي بكلمة سر؟
                    writer.Write(Path.GetFileName(inputPath));
                    writer.Write(data.Length);
                    writer.Write(frequencies.Count);

                    foreach (var pair in frequencies)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }

                    progressReporter.ReportProgress(90);

                    // Encrypt compressed data if password provided
                    if (!string.IsNullOrEmpty(password))
                    {
                        compressedData = PasswordProtection.Encrypt(compressedData, password);
                    }

                    writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                    writer.Write(compressedData); // البيانات المضغوطة (مشفرة إذا كانت محمية)
                }

                progressReporter.ReportProgress(95);

                // حساب نسبة الضغط
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = new FileInfo(outputPath).Length;
                compressionRatio = (originalSize - compressedSize) / (double)originalSize * 100;

                progressReporter.ReportProgress(100);
            }, cancellationToken);
        }

        public async Task DecompressAsync(string inputPath, string outputPath, string password = null,
                                        Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                using (var fs = new FileStream(inputPath, FileMode.Open))
                using (var reader = new BinaryReader(fs))
                {
                    string signature = reader.ReadString();
                    
                    // Handle both single file and multi-file archives
                    if (signature == "HUFFMAN_MULTI")
                    {
                        // This is a multi-file archive, extract the first file
                        fs.Position = 0; // Reset to beginning
                        using (var reader2 = new BinaryReader(fs))
                        {
                            reader2.ReadString(); // Skip signature
                            bool isPasswordProtected = reader2.ReadBoolean();
                            if (isPasswordProtected && string.IsNullOrEmpty(password))
                                throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                            int fileCount = reader2.ReadInt32();
                            if (fileCount == 0)
                                throw new InvalidDataException("الأرشيف فارغ");

                            // Extract the first file
                            string fileName = reader2.ReadString();
                            int originalSize = reader2.ReadInt32();
                            int freqCount = reader2.ReadInt32();
                            var frequencies = new Dictionary<byte, int>();

                            for (int i = 0; i < freqCount; i++)
                                frequencies[reader2.ReadByte()] = reader2.ReadInt32();

                            int compressedDataSize = reader2.ReadInt32();
                            byte[] compressedData = reader2.ReadBytes(compressedDataSize);

                            // Decrypt if password protected
                            if (isPasswordProtected)
                            {
                                compressedData = PasswordProtection.Decrypt(compressedData, password);
                            }

                            var root = BuildHuffmanTree(frequencies);
                            var decompressedData = new byte[originalSize];
                            var currentNode = root;
                            int bitIndex = 0;
                            byte currentByte = 0;
                            int dataIndex = 0;
                            int byteIndex = 0;

                            while (dataIndex < originalSize && byteIndex < compressedData.Length)
                            {
                                CheckPauseState(cancellationToken);

                                if (bitIndex == 0)
                                {
                                    currentByte = compressedData[byteIndex++];
                                    bitIndex = 8;
                                }

                                int bit = (currentByte >> 7) & 1;
                                currentByte <<= 1;
                                bitIndex--;

                                currentNode = (bit == 0) ? currentNode.Left : currentNode.Right;

                                if (currentNode.IsLeaf)
                                {
                                    decompressedData[dataIndex++] = currentNode.Symbol;
                                    currentNode = root;
                                }

                                // Update progress for decompression
                                if (dataIndex % 1000 == 0 || dataIndex == originalSize)
                                {
                                    int progress = (int)((dataIndex * 100.0) / originalSize);
                                    progressCallback?.Invoke(progress);
                                }
                            }

                            File.WriteAllBytes(outputPath, decompressedData);
                        }
                    }
                    else if (signature == "HUFFMAN")
                    {
                        // This is a single file archive (legacy format)
                        bool isPasswordProtected = reader.ReadBoolean();
                        if (isPasswordProtected && string.IsNullOrEmpty(password))
                            throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                        string fileName = reader.ReadString();
                        int originalSize = reader.ReadInt32();
                        int freqCount = reader.ReadInt32();
                        var frequencies = new Dictionary<byte, int>();

                        for (int i = 0; i < freqCount; i++)
                            frequencies[reader.ReadByte()] = reader.ReadInt32();

                        int compressedDataSize = reader.ReadInt32();
                        byte[] compressedData = reader.ReadBytes(compressedDataSize);

                        // Decrypt if password protected
                        if (isPasswordProtected)
                        {
                            compressedData = PasswordProtection.Decrypt(compressedData, password);
                        }

                        var root = BuildHuffmanTree(frequencies);
                        var decompressedData = new byte[originalSize];
                        var currentNode = root;
                        int bitIndex = 0;
                        byte currentByte = 0;
                        int dataIndex = 0;
                        int byteIndex = 0;
                        long totalBits = originalSize * 8;
                        long bitsProcessed = 0;

                        while (dataIndex < originalSize && byteIndex < compressedData.Length)
                        {
                            CheckPauseState(cancellationToken);

                            if (bitIndex == 0)
                            {
                                currentByte = compressedData[byteIndex++];
                                bitIndex = 8;
                            }

                            int bit = (currentByte >> 7) & 1;
                            currentByte <<= 1;
                            bitIndex--;

                            currentNode = (bit == 0) ? currentNode.Left : currentNode.Right;

                            if (currentNode.IsLeaf)
                            {
                                decompressedData[dataIndex++] = currentNode.Symbol;
                                currentNode = root;
                            }

                            bitsProcessed++;
                            if (bitsProcessed % 1000 == 0 || dataIndex == originalSize)
                                progressCallback?.Invoke((int)((dataIndex * 100.0) / originalSize));
                        }

                        File.WriteAllBytes(outputPath, decompressedData);
                    }
                    else
                    {
                        throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية هوفمان");
                    }
                }
            }, cancellationToken);
        }

        private void CheckPauseState(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (isPaused)
            {
                lock (pauseLock)
                {
                    // Check for cancellation before waiting
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Use timeout to periodically check for cancellation
                    Monitor.Wait(pauseLock, 500); // Wait for 500ms max, then check again
                    
                    // Check for cancellation after waiting
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        private Dictionary<byte, int> CalculateFrequencies(byte[] data)
        {
            return data.AsParallel()
                      .GroupBy(b => b)
                      .ToDictionary(g => g.Key, g => g.Count());
        }

        private HuffmanNode BuildHuffmanTree(Dictionary<byte, int> frequencies)
        {
            var nodes = frequencies.Select(p => new HuffmanNode { Symbol = p.Key, Frequency = p.Value }).ToList();

            while (nodes.Count > 1)
            {
                nodes.Sort((a, b) => a.Frequency.CompareTo(b.Frequency));
                var left = nodes[0];
                var right = nodes[1];
                nodes.RemoveRange(0, 2);
                nodes.Add(new HuffmanNode { Frequency = left.Frequency + right.Frequency, Left = left, Right = right });
            }

            return nodes[0];
        }

        private Dictionary<byte, string> BuildCodeTable(HuffmanNode root)
        {
            var codes = new Dictionary<byte, string>();
            TraverseTree(root, "", codes);
            return codes;
        }

        private void TraverseTree(HuffmanNode node, string code, Dictionary<byte, string> codes)
        {
            if (node == null) return;
            if (node.IsLeaf) codes[node.Symbol] = code;
            else
            {
                TraverseTree(node.Left, code + "0", codes);
                TraverseTree(node.Right, code + "1", codes);
            }
        }
    }
}