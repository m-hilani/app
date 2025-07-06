using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FileCompressorApp
{
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


        public async Task CompressMultipleAsync(Dictionary<string, string> filePaths, string outputPath, string password = null,
                                         Action<int> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("HUFFMAN_MULTI"); // توقيع خاص بالملفات المتعددة
                    writer.Write(filePaths.Count); // عدد الملفات

                    foreach (var file in filePaths)
                    {
                        byte[] data = File.ReadAllBytes(file.Key);
                        var frequencies = CalculateFrequencies(data);
                        var root = BuildHuffmanTree(frequencies);
                        var codes = BuildCodeTable(root);

                        writer.Write(Path.GetFileName(file.Key)); // اسم الملف
                        writer.Write(data.Length); // حجم الملف الأصلي
                        writer.Write(frequencies.Count); // عدد الترددات

                        foreach (var pair in frequencies)
                        {
                            writer.Write(pair.Key);
                            writer.Write(pair.Value);
                        }

                        // First, compress the data to a memory stream to get the exact size
                        using (var compressedDataStream = new MemoryStream())
                        using (var tempWriter = new BinaryWriter(compressedDataStream))
                        {
                            byte buffer = 0;
                            int bufferLength = 0;

                            foreach (byte b in data)
                            {
                                CheckPauseState(cancellationToken);
                                string code = codes[b];
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
                            }

                            if (bufferLength > 0)
                            {
                                buffer <<= (8 - bufferLength);
                                tempWriter.Write(buffer);
                            }

                            // Write the compressed data size, then the compressed data
                            byte[] compressedData = compressedDataStream.ToArray();
                            writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                            writer.Write(compressedData); // البيانات المضغوطة
                        }
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
                    if (reader.ReadString() != "HUFFMAN_MULTI")
                        throw new InvalidDataException("هذا ليس ملف مضغوط متعدد");

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

                        int compressedDataSize = reader.ReadInt32(); // قراءة حجم البيانات المضغوطة
                        var root = BuildHuffmanTree(frequencies);
                        var decompressedData = new byte[originalSize];
                        var currentNode = root;
                        int bitIndex = 0;
                        byte currentByte = 0;
                        int dataIndex = 0;

                        while (dataIndex < originalSize)
                        {
                            CheckPauseState(cancellationToken);

                            if (bitIndex == 0)
                            {
                                currentByte = reader.ReadByte();
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
                        }

                        if (fileName.Equals(fileToExtract, StringComparison.OrdinalIgnoreCase))
                        {
                            File.WriteAllBytes(outputPath, decompressedData);
                            foundFileName = fileName;
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
                        
                        // Read and skip compressed data using the exact size
                        int compressedDataSize = reader.ReadInt32(); // قراءة حجم البيانات المضغوطة
                        reader.ReadBytes(compressedDataSize); // تخطي البيانات المضغوطة
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
                byte[] data = File.ReadAllBytes(inputPath);
                var frequencies = CalculateFrequencies(data);
                var root = BuildHuffmanTree(frequencies);
                var codes = BuildCodeTable(root);

                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write("HUFFMAN"); // التوقيع
                    writer.Write(Path.GetFileName(inputPath));
                    writer.Write(data.Length);
                    writer.Write(frequencies.Count);

                    foreach (var pair in frequencies)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }

                    // First, compress the data to a memory stream to get the exact size
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

                            if (i % 100 == 0)
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

                        // Write the compressed data size, then the compressed data
                        byte[] compressedData = compressedDataStream.ToArray();
                        writer.Write(compressedData.Length); // حجم البيانات المضغوطة
                        writer.Write(compressedData); // البيانات المضغوطة
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
                    if (reader.ReadString() != "HUFFMAN")
                        throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية هوفمان");

                    string fileName = reader.ReadString();
                    int originalSize = reader.ReadInt32();
                    int freqCount = reader.ReadInt32();
                    var frequencies = new Dictionary<byte, int>();

                    for (int i = 0; i < freqCount; i++)
                        frequencies[reader.ReadByte()] = reader.ReadInt32();

                    int compressedDataSize = reader.ReadInt32(); // قراءة حجم البيانات المضغوطة
                    var root = BuildHuffmanTree(frequencies);
                    var decompressedData = new byte[originalSize];
                    var currentNode = root;
                    int bitIndex = 0;
                    byte currentByte = 0;
                    int dataIndex = 0;
                    long totalBits = originalSize * 8;
                    long bitsProcessed = 0;

                    while (dataIndex < originalSize)
                    {
                        CheckPauseState(cancellationToken);

                        if (bitIndex == 0)
                        {
                            currentByte = reader.ReadByte();
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
                        if (bitsProcessed % 1000 == 0)
                            progressCallback?.Invoke((int)((bitsProcessed * 100.0) / totalBits));
                    }

                    File.WriteAllBytes(outputPath, decompressedData);
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
                    Monitor.Wait(pauseLock);
                }
                cancellationToken.ThrowIfCancellationRequested();
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