# تقرير تقني مفصل - تطبيق ضغط الملفات

## نظرة عامة على التطبيق

### الهدف

تطبيق Windows Forms لضغط الملفات يدعم خوارزميتين رئيسيتين:

- **خوارزمية هوفمان (Huffman)**: ضغط بلا خسارة باستخدام الترميز المتغير
- **خوارزمية فانو-شانون (Fano-Shannon)**: ضغط بلا خسارة باستخدام التقسيم الثنائي

### المكونات الرئيسية

1. **Form1.cs**: واجهة المستخدم والتحكم الرئيسي
2. **CompressionAlgorithms.cs**: تنفيذ خوارزمية هوفمان
3. **FanoShannonCompressor.cs**: تنفيذ خوارزمية فانو-شانون
4. **Program.cs**: نقطة البداية والتكامل مع النظام

---

## 1. خوارزمية هوفمان (Huffman Coding)

### المبدأ النظري

خوارزمية هوفمان تقوم بإنشاء شجرة ثنائية بناءً على تردد الرموز في البيانات، حيث الرموز الأكثر تكراراً تحصل على رموز أقصر.

### السيودو كود

```
HUFFMAN_COMPRESS(data):
    1. حساب ترددات الرموز
    2. إنشاء عقد ورقية لكل رمز
    3. بناء شجرة هوفمان:
       - ترتيب العقد حسب التردد
       - دمج أقل عقدتين تردداً
       - تكرار حتى تبقى عقدة واحدة (الجذر)
    4. إنشاء جدول الرموز من الشجرة
    5. ضغط البيانات باستخدام الجدول
```

### التنفيذ التفصيلي

#### 1. فئة العقدة (HuffmanNode)

```csharp
private class HuffmanNode : IComparable<HuffmanNode>
{
    public byte Symbol { get; set; }        // الرمز المخزن
    public int Frequency { get; set; }      // تردد الرمز
    public HuffmanNode Left { get; set; }   // العقدة اليسرى
    public HuffmanNode Right { get; set; }  // العقدة اليمنى
    public bool IsLeaf => Left == null && Right == null;  // هل هي عقدة ورقية؟
    public int CompareTo(HuffmanNode other) => Frequency.CompareTo(other.Frequency);
}
```

**الشرح:**

- `Symbol`: يحتوي على البايت المراد ضغطه
- `Frequency`: تردد ظهور هذا البايت في البيانات
- `Left/Right`: مؤشرات للعقد الفرعية في الشجرة
- `IsLeaf`: تحدد إذا كانت العقدة ورقية (تحتوي على رمز)
- `CompareTo`: للمقارنة بين العقد حسب التردد

#### 2. حساب الترددات

```csharp
private Dictionary<byte, int> CalculateFrequencies(byte[] data)
{
    return data.AsParallel()
              .GroupBy(b => b)
              .ToDictionary(g => g.Key, g => g.Count());
}
```

**الشرح:**

- `AsParallel()`: تسريع المعالجة عبر المعالجة المتوازية
- `GroupBy(b => b)`: تجميع البايتات المتشابهة
- `ToDictionary()`: تحويل النتيجة إلى قاموس (بايت → تردد)

#### 3. بناء شجرة هوفمان

```csharp
private HuffmanNode BuildHuffmanTree(Dictionary<byte, int> frequencies)
{
    var queue = new SortedSet<HuffmanNode>();

    // إنشاء عقدة لكل رمز
    foreach (var pair in frequencies)
    {
        queue.Add(new HuffmanNode { Symbol = pair.Key, Frequency = pair.Value });
    }

    // بناء الشجرة
    while (queue.Count > 1)
    {
        var left = queue.Min;   // أقل تردد
        queue.Remove(left);
        var right = queue.Min;  // ثاني أقل تردد
        queue.Remove(right);

        // إنشاء عقدة جديدة
        var parent = new HuffmanNode
        {
            Frequency = left.Frequency + right.Frequency,
            Left = left,
            Right = right
        };
        queue.Add(parent);
    }

    return queue.First();
}
```

**الشرح:**

- `SortedSet`: مجموعة مرتبة تلقائياً حسب التردد
- نأخذ دائماً أقل عقدتين في التردد
- ننشئ عقدة والد بمجموع الترددات
- نكرر حتى تبقى عقدة واحدة (الجذر)

#### 4. إنشاء جدول الرموز

```csharp
private Dictionary<byte, string> BuildCodeTable(HuffmanNode root)
{
    var codes = new Dictionary<byte, string>();
    if (root.IsLeaf)
    {
        codes[root.Symbol] = "0";  // حالة خاصة: رمز واحد فقط
    }
    else
    {
        TraverseTree(root, "", codes);
    }
    return codes;
}

private void TraverseTree(HuffmanNode node, string code, Dictionary<byte, string> codes)
{
    if (node.IsLeaf)
    {
        codes[node.Symbol] = code;
        return;
    }

    if (node.Left != null)
        TraverseTree(node.Left, code + "0", codes);
    if (node.Right != null)
        TraverseTree(node.Right, code + "1", codes);
}
```

**الشرح:**

- `TraverseTree`: جولة في الشجرة لإنشاء الرموز
- العقدة اليسرى تحصل على "0" والعقدة اليمنى على "1"
- عند الوصول لعقدة ورقية، نحفظ الرمز المتكون

#### 5. عملية الضغط

```csharp
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
            // كتابة الهيدر
            writer.Write("HUFFMAN");                    // التوقيع
            writer.Write(!string.IsNullOrEmpty(password)); // هل محمي بكلمة سر؟
            writer.Write(Path.GetFileName(inputPath));   // اسم الملف
            writer.Write(data.Length);                   // الحجم الأصلي
            writer.Write(frequencies.Count);             // عدد الرموز

            // كتابة جدول الترددات
            foreach (var pair in frequencies)
            {
                writer.Write(pair.Key);      // الرمز
                writer.Write(pair.Value);    // التردد
            }

            // ضغط البيانات
            using (var compressedStream = new MemoryStream())
            using (var tempWriter = new BinaryWriter(compressedStream))
            {
                byte buffer = 0;
                int bufferLength = 0;

                foreach (byte b in data)
                {
                    string code = codes[b];
                    foreach (char bit in code)
                    {
                        // تجميع البتات في بايت
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

                // كتابة البتات المتبقية
                if (bufferLength > 0)
                {
                    buffer <<= (8 - bufferLength);
                    tempWriter.Write(buffer);
                }

                // تشفير البيانات إذا كان هناك كلمة سر
                byte[] compressedData = compressedStream.ToArray();
                if (!string.IsNullOrEmpty(password))
                {
                    compressedData = PasswordProtection.Encrypt(compressedData, password);
                }

                writer.Write(compressedData.Length);
                writer.Write(compressedData);
            }
        }
    }, cancellationToken);
}
```

**الشرح سطر بسطر:**

- `File.ReadAllBytes()`: قراءة الملف كمصفوفة بايتات
- `CalculateFrequencies()`: حساب تردد كل بايت
- `BuildHuffmanTree()`: بناء شجرة هوفمان
- `BuildCodeTable()`: إنشاء جدول الرموز
- `writer.Write("HUFFMAN")`: كتابة توقيع الملف للتعرف عليه
- `writer.Write(!string.IsNullOrEmpty(password))`: علامة الحماية بكلمة السر
- `writer.Write(Path.GetFileName(inputPath))`: حفظ اسم الملف الأصلي
- `writer.Write(data.Length)`: حفظ الحجم الأصلي للتحقق
- `writer.Write(frequencies.Count)`: عدد الرموز المختلفة
- الحلقة الأولى: حفظ جدول الترددات (مطلوب لفك الضغط)
- الحلقة الثانية: تحويل كل بايت إلى رمز هوفمان
- `buffer`: مؤقت لتجميع البتات في بايت
- `bufferLength`: عدد البتات المتراكمة في المؤقت
- `(buffer << 1)`: إزاحة البتات لليسار
- `(bit == '1' ? 1 : 0)`: تحويل الحرف إلى بت
- `buffer <<= (8 - bufferLength)`: ملء البتات المتبقية بالأصفار
- `PasswordProtection.Encrypt()`: تشفير البيانات إذا كان هناك كلمة سر

#### 6. عملية فك الضغط

```csharp
public async Task DecompressAsync(string inputPath, string outputPath, string password = null,
                                Action<int> progressCallback = null, CancellationToken cancellationToken = default)
{
    await Task.Run(() =>
    {
        using (var fs = new FileStream(inputPath, FileMode.Open))
        using (var reader = new BinaryReader(fs))
        {
            string signature = reader.ReadString();

            if (signature == "HUFFMAN" || signature == "HUFFMAN_MULTI")
            {
                // قراءة الهيدر
                bool isPasswordProtected = reader.ReadBoolean();
                if (isPasswordProtected && string.IsNullOrEmpty(password))
                    throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                if (signature == "HUFFMAN_MULTI")
                {
                    int fileCount = reader.ReadInt32();
                    // استخراج الملف الأول فقط
                }

                string fileName = reader.ReadString();
                int originalSize = reader.ReadInt32();
                int freqCount = reader.ReadInt32();

                // قراءة جدول الترددات
                var frequencies = new Dictionary<byte, int>();
                for (int i = 0; i < freqCount; i++)
                {
                    frequencies[reader.ReadByte()] = reader.ReadInt32();
                }

                // قراءة البيانات المضغوطة
                int compressedDataSize = reader.ReadInt32();
                byte[] compressedData = reader.ReadBytes(compressedDataSize);

                // فك التشفير إذا كان محمي
                if (isPasswordProtected)
                {
                    compressedData = PasswordProtection.Decrypt(compressedData, password);
                }

                // إعادة بناء شجرة هوفمان
                var root = BuildHuffmanTree(frequencies);
                var decompressedData = new byte[originalSize];
                var currentNode = root;

                int bitIndex = 0;
                byte currentByte = 0;
                int dataIndex = 0;
                int byteIndex = 0;

                // فك الضغط
                while (dataIndex < originalSize && byteIndex < compressedData.Length)
                {
                    // قراءة بايت جديد عند الحاجة
                    if (bitIndex == 0)
                    {
                        currentByte = compressedData[byteIndex++];
                        bitIndex = 8;
                    }

                    // قراءة البت الأعلى
                    int bit = (currentByte >> 7) & 1;
                    currentByte <<= 1;
                    bitIndex--;

                    // التنقل في الشجرة
                    currentNode = (bit == 0) ? currentNode.Left : currentNode.Right;

                    // إذا وصلنا لعقدة ورقية
                    if (currentNode.IsLeaf)
                    {
                        decompressedData[dataIndex++] = currentNode.Symbol;
                        currentNode = root;  // العودة للجذر
                    }
                }

                File.WriteAllBytes(outputPath, decompressedData);
            }
        }
    }, cancellationToken);
}
```

**الشرح:**

- `signature`: التحقق من نوع الملف
- إعادة بناء شجرة هوفمان من جدول الترددات
- `currentNode`: العقدة الحالية في الشجرة
- `bitIndex`: موقع البت الحالي في البايت
- `(currentByte >> 7) & 1`: استخراج البت الأعلى
- `currentByte <<= 1`: إزاحة البتات لليسار
- التنقل: 0 = يسار، 1 = يمين
- عند الوصول لعقدة ورقية: حفظ الرمز والعودة للجذر

---

## 2. خوارزمية فانو-شانون (Fano-Shannon Coding)

### المبدأ النظري

خوارزمية فانو-شانون تقوم بتقسيم الرموز إلى مجموعتين بحيث يكون مجموع الترددات في كل مجموعة متقارباً قدر الإمكان.

### السيودو كود

```
FANO_SHANNON_COMPRESS(data):
    1. حساب ترددات الرموز
    2. ترتيب الرموز حسب التردد (تنازلي)
    3. بناء الرموز بشكل تكراري:
       - تقسيم المجموعة إلى نصفين متوازنين
       - إعطاء النصف الأول "0" والثاني "1"
       - تكرار العملية لكل نصف
    4. ضغط البيانات باستخدام الجدول
```

### التنفيذ التفصيلي

#### 1. فئة العقدة (ShannonNode)

```csharp
private class ShannonNode
{
    public byte Symbol { get; set; }      // الرمز
    public int Frequency { get; set; }    // التردد
    public string Code { get; set; }      // الرمز الثنائي
}
```

**الشرح:**

- `Symbol`: البايت المراد ترميزه
- `Frequency`: تردد ظهور هذا البايت
- `Code`: الرمز الثنائي المعطى لهذا البايت

#### 2. بناء رموز فانو-شانون

```csharp
private Dictionary<byte, string> BuildShannonCodes(Dictionary<byte, int> frequencies)
{
    var nodes = frequencies.Select(p => new ShannonNode
    {
        Symbol = p.Key,
        Frequency = p.Value,
        Code = ""
    }).OrderByDescending(n => n.Frequency).ToList();

    // حالات خاصة
    if (nodes.Count == 0)
        return new Dictionary<byte, string>();

    if (nodes.Count == 1)
    {
        nodes[0].Code = "0";
        return nodes.ToDictionary(n => n.Symbol, n => n.Code);
    }

    BuildCodesRecursive(nodes, 0, nodes.Count - 1);
    return nodes.ToDictionary(n => n.Symbol, n => n.Code);
}
```

**الشرح:**

- `Select()`: تحويل القاموس إلى قائمة عقد
- `OrderByDescending()`: ترتيب حسب التردد (تنازلي)
- معالجة الحالات الخاصة (0 أو 1 رمز)
- `BuildCodesRecursive()`: بناء الرموز بشكل تكراري

#### 3. البناء التكراري للرموز

```csharp
private void BuildCodesRecursive(List<ShannonNode> nodes, int start, int end)
{
    if (start >= end) return;

    // حالة قاعدية: عقدتان
    if (end - start == 1)
    {
        nodes[start].Code += "0";
        nodes[end].Code += "1";
        return;
    }

    // حساب إجمالي التردد
    int totalFreq = 0;
    for (int i = start; i <= end; i++)
        totalFreq += nodes[i].Frequency;

    // العثور على نقطة التقسيم المثلى
    int currentSum = 0;
    int bestSplit = start;
    int bestDiff = int.MaxValue;

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

    // إعطاء الرموز
    for (int i = start; i <= bestSplit; i++)
        nodes[i].Code += "0";

    for (int i = bestSplit + 1; i <= end; i++)
        nodes[i].Code += "1";

    // استدعاء تكراري للمجموعتين
    BuildCodesRecursive(nodes, start, bestSplit);
    BuildCodesRecursive(nodes, bestSplit + 1, end);
}
```

**الشرح:**

- `totalFreq`: مجموع ترددات المجموعة
- `currentSum`: مجموع ترددات المجموعة اليسرى
- `leftSum/rightSum`: ترددات المجموعتين
- `diff`: الفرق بين المجموعتين
- `bestSplit`: نقطة التقسيم الأمثل
- إعطاء "0" للمجموعة اليسرى و "1" للمجموعة اليمنى
- استدعاء تكراري لتقسيم كل مجموعة

---

## 3. نظام حماية كلمة السر

### التشفير باستخدام AES-256

```csharp
public static class PasswordProtection
{
    private const int SaltSize = 16;      // حجم الملح
    private const int IvSize = 16;        // حجم متجه التهيئة
    private const int KeySize = 32;       // حجم المفتاح (256 بت)
    private const int Iterations = 10000; // عدد التكرارات

    public static byte[] Encrypt(byte[] data, string password)
    {
        if (string.IsNullOrEmpty(password))
            return data;

        // توليد ملح ومتجه تهيئة عشوائي
        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(iv);
        }

        // اشتقاق المفتاح من كلمة السر
        byte[] key = DeriveKey(password, salt);

        // تشفير البيانات
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor())
            using (var msEncrypt = new MemoryStream())
            {
                // كتابة الملح ومتجه التهيئة أولاً
                msEncrypt.Write(salt, 0, salt.Length);
                msEncrypt.Write(iv, 0, iv.Length);

                // تشفير البيانات
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    csEncrypt.Write(data, 0, data.Length);
                }

                return msEncrypt.ToArray();
            }
        }
    }
}
```

**الشرح:**

- `SaltSize`: حجم الملح المستخدم لتقوية كلمة السر
- `IvSize`: حجم متجه التهيئة لضمان التشفير المختلف
- `KeySize`: حجم المفتاح (256 بت = 32 بايت)
- `Iterations`: عدد التكرارات لاشتقاق المفتاح
- `RandomNumberGenerator`: مولد أرقام عشوائية آمن
- `PBKDF2`: اشتقاق المفتاح من كلمة السر
- `AES-256-CBC`: خوارزمية التشفير المتماثل
- `PKCS7`: نمط الحشو للبيانات

### اشتقاق المفتاح

```csharp
private static byte[] DeriveKey(string password, byte[] salt)
{
    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
    {
        return pbkdf2.GetBytes(KeySize);
    }
}
```

**الشرح:**

- `Rfc2898DeriveBytes`: تنفيذ PBKDF2
- `salt`: الملح العشوائي لتقوية كلمة السر
- `Iterations`: عدد التكرارات لجعل الهجمات أصعب

---

## 4. واجهة المستخدم (Form1.cs)

### التحكم في العمليات

```csharp
private async Task StartOperationAsync(Func<Task> operation)
{
    try
    {
        // تعطيل أزرار العمليات
        DisableOperationButtons();
        btnPauseResume.Enabled = true;
        btnCancel.Enabled = true;

        cts = new CancellationTokenSource();

        await operation();

        MessageBox.Show("تمت العملية بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (OperationCanceledException)
    {
        MessageBox.Show("تم إلغاء العملية", "معلومة", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (UnauthorizedAccessException ex)
    {
        MessageBox.Show(ex.Message, "خطأ في كلمة السر", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    finally
    {
        ResetUI();
    }
}
```

**الشرح:**

- `Func<Task>`: دالة لتنفيذ العملية المطلوبة
- `DisableOperationButtons()`: تعطيل أزرار العمليات أثناء التنفيذ
- `CancellationTokenSource`: للتحكم في إلغاء العملية
- معالجة الاستثناءات المختلفة
- `ResetUI()`: إعادة تعيين الواجهة

### الإيقاف المؤقت والإلغاء

```csharp
private void btnPauseResume_Click(object sender, EventArgs e)
{
    if (currentAlgorithm != null)
    {
        if (isPaused)
        {
            // استئناف العملية
            if (currentAlgorithm is HuffmanCompressor huffman)
                huffman.Resume();
            else if (currentAlgorithm is FanoShannonCompressor fano)
                fano.Resume();

            btnPauseResume.Text = "إيقاف مؤقت";
            isPaused = false;
        }
        else
        {
            // إيقاف مؤقت
            if (currentAlgorithm is HuffmanCompressor huffman)
                huffman.Pause();
            else if (currentAlgorithm is FanoShannonCompressor fano)
                fano.Pause();

            btnPauseResume.Text = "استئناف";
            isPaused = true;
        }
    }
}
```

**الشرح:**

- التحقق من نوع الخوارزمية الحالية
- استدعاء الدالة المناسبة للإيقاف/الاستئناف
- تحديث نص الزر والحالة

### التكامل مع القائمة السياقية

```csharp
private async Task HandleContextMenuOperation()
{
    try
    {
        switch (contextMenuOperation.ToLower())
        {
            case "/compress":
            case "-compress":
                await AutoCompressFile(contextMenuFilePath);
                break;
            case "/decompress":
            case "-decompress":
                await AutoDecompressFile(contextMenuFilePath);
                break;
            case "/compressfolder":
            case "-compressfolder":
                await AutoCompressFolder(contextMenuFilePath);
                break;
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"خطأ في العملية: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

**الشرح:**

- `contextMenuOperation`: العملية المطلوبة من القائمة السياقية
- `contextMenuFilePath`: مسار الملف المحدد
- تنفيذ العملية المناسبة حسب النوع

---

## 5. إدارة الملفات المتعددة

### ضغط الملفات المتعددة

```csharp
public async Task CompressMultipleAsync(Dictionary<string, string> filePaths, string outputPath, string password = null,
                                Action<int> progressCallback = null, CancellationToken cancellationToken = default)
{
    await Task.Run(() =>
    {
        using (var fs = new FileStream(outputPath, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write("HUFFMAN_MULTI");    // توقيع الملفات المتعددة
            writer.Write(!string.IsNullOrEmpty(password)); // حماية كلمة السر
            writer.Write(filePaths.Count);     // عدد الملفات

            foreach (var file in filePaths)
            {
                byte[] data = File.ReadAllBytes(file.Key);
                var frequencies = CalculateFrequencies(data);
                var root = BuildHuffmanTree(frequencies);
                var codes = BuildCodeTable(root);

                writer.Write(Path.GetFileName(file.Key)); // اسم الملف
                writer.Write(data.Length);                // الحجم الأصلي
                writer.Write(frequencies.Count);          // عدد الترددات

                // كتابة جدول الترددات
                foreach (var pair in frequencies)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }

                // ضغط البيانات
                // ... (نفس آلية الضغط العادية)
            }
        }
    }, cancellationToken);
}
```

**الشرح:**

- `Dictionary<string, string>`: (مسار الملف، اسم الملف)
- `"HUFFMAN_MULTI"`: توقيع خاص بالملفات المتعددة
- حفظ عدد الملفات في الهيدر
- معالجة كل ملف بشكل منفصل
- حفظ معلومات كل ملف (الاسم، الحجم، الترددات)

### استخراج ملف واحد

```csharp
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
            int fileCount = reader.ReadInt32();

            // البحث عن الملف المطلوب
            for (int i = 0; i < fileCount; i++)
            {
                string fileName = reader.ReadString();
                int originalSize = reader.ReadInt32();
                int freqCount = reader.ReadInt32();

                // قراءة جدول الترددات
                var frequencies = new Dictionary<byte, int>();
                for (int j = 0; j < freqCount; j++)
                    frequencies[reader.ReadByte()] = reader.ReadInt32();

                int compressedDataSize = reader.ReadInt32();
                byte[] compressedData = reader.ReadBytes(compressedDataSize);

                // إذا كان هذا هو الملف المطلوب
                if (fileName.Equals(fileToExtract, StringComparison.OrdinalIgnoreCase))
                {
                    // فك التشفير
                    if (isPasswordProtected)
                    {
                        compressedData = PasswordProtection.Decrypt(compressedData, password);
                    }

                    // فك الضغط
                    var root = BuildHuffmanTree(frequencies);
                    // ... (آلية فك الضغط)

                    File.WriteAllBytes(outputPath, decompressedData);
                    return fileName;
                }
            }

            throw new FileNotFoundException("الملف المطلوب غير موجود في الأرشيف");
        }
    }, cancellationToken);
}
```

**الشرح:**

- التحقق من توقيع الملفات المتعددة
- البحث عن الملف المطلوب بالاسم
- تخطي الملفات الأخرى
- فك ضغط الملف المطلوب فقط

---

## 6. تحسين الأداء

### المعالجة المتوازية

```csharp
private Dictionary<byte, int> CalculateFrequencies(byte[] data)
{
    return data.AsParallel()
              .GroupBy(b => b)
              .ToDictionary(g => g.Key, g => g.Count());
}
```

**الشرح:**

- `AsParallel()`: توزيع المعالجة على عدة خيوط
- تسريع حساب الترددات للملفات الكبيرة

### إدارة الذاكرة

```csharp
using (var compressedDataStream = new MemoryStream())
using (var tempWriter = new BinaryWriter(compressedDataStream))
{
    // المعالجة...
}
```

**الشرح:**

- `using`: تضمن تحرير الموارد تلقائياً
- `MemoryStream`: معالجة البيانات في الذاكرة بدلاً من الملفات المؤقتة

### مراقبة التقدم

```csharp
if (i % 100 == 0)
{
    int progress = (int)((i * 100.0) / totalBytes);
    progressCallback?.Invoke(progress);
}
```

**الشرح:**

- تحديث التقدم كل 100 عنصر
- `progressCallback?.Invoke()`: استدعاء آمن للدالة

---

## 7. إحصائيات الأداء

### حساب نسبة الضغط

```csharp
public async Task<double> EstimateCompressionRatioAsync(string inputPath)
{
    return await Task.Run(() =>
    {
        byte[] data = File.ReadAllBytes(inputPath);
        var frequencies = CalculateFrequencies(data);
        var codes = BuildShannonCodes(frequencies);

        long totalBits = 0;
        foreach (var pair in frequencies)
        {
            totalBits += pair.Value * codes[pair.Key].Length;
        }

        double compressedSize = totalBits / 8.0;
        double originalSize = data.Length;

        return ((originalSize - compressedSize) / originalSize) * 100;
    });
}
```

**الشرح:**

- حساب العدد الإجمالي للبتات بعد الضغط
- `pair.Value * codes[pair.Key].Length`: تردد × طول الرمز
- `totalBits / 8.0`: تحويل البتات إلى بايتات
- `((originalSize - compressedSize) / originalSize) * 100`: نسبة الضغط

---

## 8. الأمان والموثوقية

### التحقق من التوقيعات

```csharp
string signature = reader.ReadString();
if (signature != "HUFFMAN" && signature != "HUFFMAN_MULTI")
{
    throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية هوفمان");
}
```

**الشرح:**

- التحقق من توقيع الملف قبل المعالجة
- دعم التوقيعات المختلفة للتوافق مع الإصدارات

### معالجة الأخطاء

```csharp
try
{
    // العملية الرئيسية
}
catch (OperationCanceledException)
{
    // إلغاء العملية
}
catch (UnauthorizedAccessException ex)
{
    // خطأ في كلمة السر
}
catch (Exception ex)
{
    // أخطاء أخرى
}
```

**الشرح:**

- معالجة محددة لكل نوع من الأخطاء
- رسائل خطأ واضحة للمستخدم

---

## 9. التحسينات المقترحة

### 1. ضغط أفضل

- **تطبيق الترميز التكيفي**: تحديث جدول الترددات أثناء الضغط
- **استخدام خوارزمية LZ77**: دمج مع هوفمان للحصول على ضغط أفضل
- **ضغط البيانات المتشابهة**: اكتشاف الأنماط المتكررة

### 2. أداء أفضل

- **معالجة الملفات الكبيرة**: قراءة وكتابة على دفعات
- **ضغط متوازي**: معالجة عدة ملفات في نفس الوقت
- **ذاكرة التخزين المؤقت**: حفظ الترددات الشائعة

### 3. مميزات إضافية

- **مقارنة الخوارزميات**: اختبار عدة خوارزميات واختيار الأفضل
- **إصلاح الملفات التالفة**: استخدام رموز تصحيح الأخطاء
- **واجهة سطر الأوامر**: للاستخدام في السكريبت

---

## خلاصة

هذا التطبيق يوفر حلاً شاملاً لضغط الملفات باستخدام خوارزميتين مختلفتين، مع دعم:

- **الحماية بكلمة السر** باستخدام AES-256
- **الضغط المتعدد** لعدة ملفات في أرشيف واحد
- **التحكم في العمليات** (إيقاف، استئناف، إلغاء)
- **التكامل مع النظام** عبر القائمة السياقية
- **واجهة سهلة الاستخدام** باللغة العربية

التطبيق يتميز بكود منظم وقابل للصيانة، مع معالجة شاملة للأخطاء وتحسينات في الأداء.
