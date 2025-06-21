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
                    writer.Write(filePaths.Count); // عدد الملفات

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

                        var codes = BuildShannonCodes(frequencies);
                        var reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);
                        var decompressedData = new byte[originalSize];
                        string currentCode = "";
                        int dataIndex = 0;

                        while (dataIndex < originalSize)
                        {
                            CheckPauseState(cancellationToken);

                            byte currentByte = reader.ReadByte();
                            for (int j = 7; j >= 0; j--)
                            {
                                int bit = (currentByte >> j) & 1;
                                currentCode += bit.ToString();

                                if (reverseCodes.ContainsKey(currentCode))
                                {
                                    decompressedData[dataIndex++] = reverseCodes[currentCode];
                                    currentCode = "";

                                    if (dataIndex >= originalSize)
                                        break;
                                }
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
                    writer.Write(Path.GetFileName(inputPath));
                    writer.Write(data.Length);
                    writer.Write(frequencies.Count);

                    foreach (var pair in frequencies)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value);
                    }

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
                                writer.Write(buffer);
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
                        writer.Write(buffer);
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
                    if (reader.ReadString() != "FANO")
                        throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية فانو-شانون");

                    string fileName = reader.ReadString();
                    int originalSize = reader.ReadInt32();
                    int freqCount = reader.ReadInt32();
                    var frequencies = new Dictionary<byte, int>();

                    for (int i = 0; i < freqCount; i++)
                        frequencies[reader.ReadByte()] = reader.ReadInt32();

                    var codes = BuildShannonCodes(frequencies);
                    var reverseCodes = codes.ToDictionary(x => x.Value, x => x.Key);
                    var decompressedData = new byte[originalSize];
                    string currentCode = "";
                    int dataIndex = 0;
                    long totalBits = originalSize * 8;
                    long bitsProcessed = 0;

                    while (dataIndex < originalSize)
                    {
                        CheckPauseState(cancellationToken);

                        byte currentByte = reader.ReadByte();
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

        private Dictionary<byte, string> BuildShannonCodes(Dictionary<byte, int> frequencies)
        {
            var nodes = frequencies.Select(p => new ShannonNode
            {
                Symbol = p.Key,
                Frequency = p.Value
            })
            .OrderByDescending(n => n.Frequency)
            .ToList();

            BuildCodesRecursive(nodes, 0, nodes.Count - 1);

            return nodes.ToDictionary(n => n.Symbol, n => n.Code);
        }

        private void BuildCodesRecursive(List<ShannonNode> nodes, int start, int end)
        {
            if (start >= end) return;

            // حساب النقطة المثلى للتقسيم
            int total = nodes.Skip(start).Take(end - start + 1).Sum(n => n.Frequency);
            int sum = 0;
            int splitIndex = start;

            for (int i = start; i <= end; i++)
            {
                sum += nodes[i].Frequency;
                if (sum >= total / 2)
                {
                    // اختيار أفضل نقطة تقسيم
                    int diff1 = Math.Abs(total - 2 * sum);
                    int diff2 = Math.Abs(total - 2 * (sum - nodes[i].Frequency));

                    splitIndex = (diff1 < diff2) ? i : i - 1;
                    break;
                }
            }

            // تعيين الأكواد للأجزاء
            for (int i = start; i <= splitIndex; i++)
                nodes[i].Code += "0";

            for (int i = splitIndex + 1; i <= end; i++)
                nodes[i].Code += "1";

            // تقسيم متكرر
            BuildCodesRecursive(nodes, start, splitIndex);
            BuildCodesRecursive(nodes, splitIndex + 1, end);
        }
    }
}