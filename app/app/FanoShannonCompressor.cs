using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompressorApp
{
    public class FanoShannonCompressor : ICompressionAlgorithm
    {
        private class ShannonNode
        {
            public byte Symbol { get; set; }
            public int Frequency { get; set; }
            public string Code { get; set; }
        }

        private double compressionRatio;
        private bool isPaused;
        private object pauseLock = new object();

        public double GetCompressionRatio() => compressionRatio;

        public void Pause() => isPaused = true;
        public void Resume() { isPaused = false; lock (pauseLock) { Monitor.Pulse(pauseLock); } }

        public async Task CompressMultipleAsync(Dictionary<string, string> filePaths, string outputPath, string password = null,
                                        Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("FANO_MULTI"); // توقيع خاص بالملفات المتعددة
                    writer.Write(!string.IsNullOrEmpty(password)); // هل الملف محمي بكلمة سر؟
                    writer.Write(filePaths.Count); // عدد الملفات

                    int totalFiles = filePaths.Count;
                    int currentFileIndex = 0;

                    foreach (var file in filePaths)
                    {
                        byte[] data = File.ReadAllBytes(file.Key);
                        var frequencies = CalculateFrequencies(data);
                        var codes = BuildShannonCodes(frequencies);

                        writer.Write(Path.GetFileName(file.Key)); // اسم الملف
                        writer.Write(data.Length); // حجم الملف الأصلي
                        writer.Write(frequencies.Count); // عدد الترددات

                        foreach (var pair in frequencies)
                        {
                            writer.Write(pair.Key);
                            writer.Write(pair.Value);
                        }

                        // Compress the data to a memory stream
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

                                // Update progress for current file within the overall progress
                                if (i % 1000 == 0 || i == totalBytes - 1)
                                {
                                    int fileProgress = (int)((i * 100.0) / totalBytes);
                                    int overallProgress = (int)((currentFileIndex * 100.0 + fileProgress) / totalFiles);
                                    progressCallback?.Invoke(overallProgress);
                                }
                            }

                            if (bufferLength > 0)
                            {
                                buffer <<= (8 - bufferLength);
                                tempWriter.Write(buffer);
                            }

                            // Encrypt compressed data if password provided
                            byte[] compressedData = compressedDataStream.ToArray();
                            if (!string.IsNullOrEmpty(password))
                            {
                                compressedData = PasswordProtection.Encrypt(compressedData, password);
                            }

                            writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                            writer.Write(compressedData); // البيانات المضغوطة (مشفرة إذا كانت محمية)
                        }

                        currentFileIndex++;
                        progressCallback?.Invoke((int)((currentFileIndex * 100.0) / totalFiles));
                    }
                }
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
                    if (reader.ReadString() != "FANO_MULTI")
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
                            var codes = BuildShannonCodes(frequencies);
                            var reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);
                            var decompressedData = new byte[originalSize];
                            string currentCode = "";
                            int dataIndex = 0;
                            int byteIndex = 0;

                            while (dataIndex < originalSize && byteIndex < compressedData.Length)
                            {
                                CheckPauseState(cancellationToken);

                                byte currentByte = compressedData[byteIndex++];
                                for (int j = 7; j >= 0; j--)
                                {
                                    int bit = (currentByte >> j) & 1;
                                    currentCode += bit.ToString();

                                    if (reverseCodes.ContainsKey(currentCode))
                                    {
                                        decompressedData[dataIndex++] = reverseCodes[currentCode];
                                        currentCode = "";

                                        // Update progress for decompression
                                        if (dataIndex % 1000 == 0 || dataIndex == originalSize)
                                        {
                                            int decompressProgress = (int)((dataIndex * 50.0) / originalSize);
                                            progressCallback?.Invoke(50 + decompressProgress);
                                        }

                                        if (dataIndex >= originalSize)
                                            break;
                                    }
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
                    if (reader.ReadString() != "FANO_MULTI")
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
                byte[] sampleData = File.ReadAllBytes(inputPath).Take(1024 * 1024).ToArray();
                var frequencies = CalculateFrequencies(sampleData);
                var codes = BuildShannonCodes(frequencies);

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
                byte[] data = File.ReadAllBytes(inputPath);
                var frequencies = CalculateFrequencies(data);
                var codes = BuildShannonCodes(frequencies);

                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("FANO"); // توقيع الملف
                    writer.Write(!string.IsNullOrEmpty(password)); // هل الملف محمي بكلمة سر؟
                    writer.Write(Path.GetFileName(inputPath));
                    writer.Write(data.Length);
                    writer.Write(frequencies.Count);

                    foreach (var pair in frequencies)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }

                    // Compress the data to a memory stream
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
                                progressCallback?.Invoke(progress);
                            }
                        }

                        if (bufferLength > 0)
                        {
                            buffer <<= (8 - bufferLength);
                            tempWriter.Write(buffer);
                        }

                        // Encrypt compressed data if password provided
                        byte[] compressedData = compressedDataStream.ToArray();
                        if (!string.IsNullOrEmpty(password))
                        {
                            compressedData = PasswordProtection.Encrypt(compressedData, password);
                        }

                        writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                        writer.Write(compressedData); // البيانات المضغوطة (مشفرة إذا كانت محمية)
                    }
                }

                // حساب نسبة الضغط
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = new FileInfo(outputPath).Length;
                compressionRatio = (originalSize - compressedSize) / (double)originalSize * 100;

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
                    if (signature == "FANO_MULTI")
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

                            var codes = BuildShannonCodes(frequencies);
                            var reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);
                            var decompressedData = new byte[originalSize];
                            string currentCode = "";
                            int dataIndex = 0;
                            int byteIndex = 0;

                            while (dataIndex < originalSize && byteIndex < compressedData.Length)
                            {
                                CheckPauseState(cancellationToken);

                                byte currentByte = compressedData[byteIndex++];
                                for (int i = 7; i >= 0; i--)
                                {
                                    int bit = (currentByte >> i) & 1;
                                    currentCode += bit.ToString();

                                    if (reverseCodes.ContainsKey(currentCode))
                                    {
                                        decompressedData[dataIndex++] = reverseCodes[currentCode];
                                        currentCode = "";

                                        // Update progress for decompression
                                        if (dataIndex % 1000 == 0 || dataIndex == originalSize)
                                        {
                                            int progress = (int)((dataIndex * 100.0) / originalSize);
                                            progressCallback?.Invoke(progress);
                                        }

                                        if (dataIndex >= originalSize)
                                            break;
                                    }
                                }
                            }

                            File.WriteAllBytes(outputPath, decompressedData);
                        }
                    }
                    else if (signature == "FANO")
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

                        var codes = BuildShannonCodes(frequencies);
                        var reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);
                        var decompressedData = new byte[originalSize];
                        string currentCode = "";
                        int dataIndex = 0;
                        int byteIndex = 0;
                        long totalBits = originalSize * 8;
                        long bitsProcessed = 0;

                        while (dataIndex < originalSize && byteIndex < compressedData.Length)
                        {
                            CheckPauseState(cancellationToken);

                            byte currentByte = compressedData[byteIndex++];
                            bitsProcessed += 8;

                            for (int i = 7; i >= 0; i--)
                            {
                                int bit = (currentByte >> i) & 1;
                                currentCode += bit.ToString();

                                if (reverseCodes.ContainsKey(currentCode))
                                {
                                    decompressedData[dataIndex++] = reverseCodes[currentCode];
                                    currentCode = "";

                                    if (dataIndex >= originalSize)
                                        break;
                                }
                            }

                            if (bitsProcessed % 1000 == 0 || dataIndex == originalSize)
                                progressCallback?.Invoke((int)((dataIndex * 100.0) / originalSize));
                        }

                        File.WriteAllBytes(outputPath, decompressedData);
                    }
                    else
                    {
                        throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية فانو-شانون");
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

        private Dictionary<byte, string> BuildShannonCodes(Dictionary<byte, int> frequencies)
        {
            var nodes = frequencies.Select(p => new ShannonNode { Symbol = p.Key, Frequency = p.Value, Code = "" })
                                  .OrderByDescending(n => n.Frequency)
                                  .ToList();

            // Handle special cases
            if (nodes.Count == 0)
                return new Dictionary<byte, string>();
            
            if (nodes.Count == 1)
            {
                // Single symbol case - assign a single bit code
                nodes[0].Code = "0";
                return nodes.ToDictionary(n => n.Symbol, n => n.Code);
            }

            BuildCodesRecursive(nodes, 0, nodes.Count - 1);
            return nodes.ToDictionary(n => n.Symbol, n => n.Code);
        }

        private void BuildCodesRecursive(List<ShannonNode> nodes, int start, int end)
        {
            if (start >= end) return;

            // Base case: two nodes
            if (end - start == 1)
            {
                nodes[start].Code += "0";
                nodes[end].Code += "1";
                return;
            }

            // Find the optimal split point
            int totalFreq = 0;
            for (int i = start; i <= end; i++)
                totalFreq += nodes[i].Frequency;

            int currentSum = 0;
            int split = start;
            int bestSplit = start;
            int bestDiff = int.MaxValue;

            // Find the split that creates the most balanced division
            for (int i = start; i < end; i++)
            {
                currentSum += nodes[i].Frequency;
                int leftSum = currentSum;
                int rightSum = totalFreq - currentSum;
                int diff = Math.Abs(leftSum - rightSum);
                
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestSplit = i;
                }
            }
            
            split = bestSplit;

            // Assign codes: left group gets "0", right group gets "1"
            for (int i = start; i <= split; i++)
                nodes[i].Code += "0";

            for (int i = split + 1; i <= end; i++)
                nodes[i].Code += "1";

            // Recursively build codes for both groups
            BuildCodesRecursive(nodes, start, split);
            BuildCodesRecursive(nodes, split + 1, end);
        }
    }
}