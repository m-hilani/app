# دليل التطوير التقني - تطبيق ضغط الملفات

## نظرة عامة على التطبيق

تطبيق ضغط الملفات هو برنامج C# يستخدم Windows Forms ويدعم خوارزميتين للضغط:

- **خوارزمية هوفمان (Huffman)**: تستخدم شجرة هوفمان لإنشاء أكواد ذات أطوال متغيرة
- **خوارزمية فانو-شانون (Fano-Shannon)**: تستخدم تقسيم الرموز بناءً على الترددات

---

## الهيكل العام للتطبيق

### 1. الملفات الرئيسية

```
app/
├── Form1.cs                     # الواجهة الرئيسية
├── Form1.designer.cs            # تصميم الواجهة
├── CompressionAlgorithms.cs     # خوارزمية هوفمان
├── FanoShannonCompressor.cs     # خوارزمية فانو-شانون
├── Program.cs                   # نقطة دخول التطبيق
└── install_context_menu.bat     # تركيب قائمة السياق
```

---

## الواجهة الرئيسية (Form1.cs)

### المتغيرات الأساسية

```csharp
private ICompressionAlgorithm currentAlgorithm;    // الخوارزمية المحددة حاليا
private CancellationTokenSource cts;              // إلغاء العمليات
private bool isPaused = false;                     // حالة الإيقاف المؤقت
private string contextMenuFilePath;                // مسار الملف من قائمة السياق
private string contextMenuOperation;               // العملية المطلوبة من قائمة السياق
```

### Constructor - منشئ الفئة

```csharp
public Form1()
{
    InitializeComponent();
    currentAlgorithm = new HuffmanCompressor(); // الخوارزمية الافتراضية
}

public Form1(string operation, string filePath) : this()
{
    contextMenuOperation = operation;  // حفظ العملية المطلوبة
    contextMenuFilePath = filePath;    // حفظ مسار الملف
}
```

**الشرح**: يوجد منشئين للفئة - الأول للاستخدام العادي والثاني لاستقبال معاملات من قائمة السياق.

---

## الميزات الرئيسية

### 1. ضغط الملفات المتعددة

```csharp
private async void btnMultiCompress_Click(object sender, EventArgs e)
{
    var ofd = new OpenFileDialog();
    ofd.Multiselect = true; // السماح بتحديد ملفات متعددة

    if (ofd.ShowDialog() == DialogResult.OK)
    {
        var sfd = new SaveFileDialog();
        sfd.Filter = currentAlgorithm is HuffmanCompressor ?
                    "ملف Huffman مضغوط|*.huff" :
                    "ملف Fano-Shannon مضغوط|*.fs";

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            // إنشاء قاموس يربط المسارات الكاملة بالمسارات النسبية
            var files = ofd.FileNames.ToDictionary(f => f, f => Path.GetFileName(f));

            await StartOperationAsync(() =>
                currentAlgorithm.CompressMultipleAsync(files, sfd.FileName,
                                                     txtPassword.Text, UpdateProgress, cts.Token));
        }
    }
}
```

**الشرح**:

- يسمح للمستخدم باختيار ملفات متعددة
- ينشئ قاموس يربط المسار الكامل للملف بالاسم فقط للتخزين
- يدعم كلاً من الملفات المفردة والمتعددة في دالة واحدة

### 2. فك الضغط التلقائي

```csharp
private async void btnDecompress_Click(object sender, EventArgs e)
{
    var ofd = new OpenFileDialog();
    ofd.Filter = "ملفات مضغوطة|*.huff;*.fs";

    if (ofd.ShowDialog() == DialogResult.OK)
    {
        // الكشف التلقائي عن نوع الخوارزمية من امتداد الملف
        ICompressionAlgorithm decompressAlgorithm;
        if (ofd.FileName.EndsWith(".huff", StringComparison.OrdinalIgnoreCase))
        {
            decompressAlgorithm = new HuffmanCompressor();
            radioHuffman.Checked = true;
        }
        else if (ofd.FileName.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
        {
            decompressAlgorithm = new FanoShannonCompressor();
            radioFanoShannon.Checked = true;
        }
        else
        {
            MessageBox.Show("نوع الملف المضغوط غير مدعوم. يجب أن يكون .huff أو .fs");
            return;
        }

        var sfd = new SaveFileDialog();
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            await StartOperationAsync(() =>
                decompressAlgorithm.DecompressAsync(ofd.FileName, sfd.FileName,
                                                  txtPassword.Text, UpdateProgress, cts.Token));
        }
    }
}
```

**الشرح**:

- يكتشف نوع الخوارزمية تلقائياً من امتداد الملف
- يحديث واجهة المستخدم لتعكس الخوارزمية المكتشفة
- يدعم كلاً من الملفات المفردة والمتعددة

### 3. استخراج ملف واحد من أرشيف

```csharp
private async void btnExtractSingle_Click(object sender, EventArgs e)
{
    var ofd = new OpenFileDialog();
    ofd.Filter = "ملفات مضغوطة|*.huff;*.fs";

    if (ofd.ShowDialog() == DialogResult.OK)
    {
        // الكشف التلقائي عن الخوارزمية
        ICompressionAlgorithm extractAlgorithm;
        if (ofd.FileName.EndsWith(".huff"))
            extractAlgorithm = new HuffmanCompressor();
        else if (ofd.FileName.EndsWith(".fs"))
            extractAlgorithm = new FanoShannonCompressor();
        else
        {
            MessageBox.Show("نوع الملف المضغوط غير مدعوم");
            return;
        }

        // الحصول على قائمة الملفات في الأرشيف
        var fileList = await extractAlgorithm.ListFilesInArchiveAsync(ofd.FileName, txtPassword.Text);

        if (fileList.Count == 0)
        {
            MessageBox.Show("لا توجد ملفات في الأرشيف!");
            return;
        }

        // عرض نموذج لاختيار الملف
        var inputForm = new ExtractSingleFileForm();
        inputForm.SetAvailableFiles(fileList);

        if (inputForm.ShowDialog() == DialogResult.OK)
        {
            // استخراج الملف المحدد
            await StartOperationAsync(() =>
                extractAlgorithm.ExtractSingleFileAsync(ofd.FileName, inputForm.FileNameToExtract,
                                                      sfd.FileName, txtPassword.Text, UpdateProgress, cts.Token));
        }
    }
}
```

**الشرح**:

- يعرض قائمة بالملفات الموجودة في الأرشيف
- يسمح للمستخدم باختيار ملف واحد للاستخراج
- يستخدم نموذج مخصص لعرض قائمة الملفات

---

## إدارة العمليات والتحكم

### إدارة العمليات غير المتزامنة

```csharp
private async Task StartOperationAsync(Func<Task> operation)
{
    try
    {
        // تعطيل أزرار العمليات وتفعيل أزرار التحكم
        DisableOperationButtons();
        btnPauseResume.Enabled = true;
        btnCancel.Enabled = true;

        cts = new CancellationTokenSource();
        await operation();

        MessageBox.Show("تمت العملية بنجاح!", "نجاح");
    }
    catch (OperationCanceledException)
    {
        MessageBox.Show("تم إلغاء العملية", "معلومة");
    }
    catch (UnauthorizedAccessException ex)
    {
        MessageBox.Show(ex.Message, "خطأ في كلمة السر");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ");
    }
    finally
    {
        ResetUI(); // إعادة تعيين الواجهة
    }
}
```

### الإيقاف المؤقت والاستئناف

```csharp
private void btnPauseResume_Click(object sender, EventArgs e)
{
    if (currentAlgorithm is HuffmanCompressor huffman)
    {
        if (isPaused)
        {
            huffman.Resume();
            btnPauseResume.Text = "إيقاف مؤقت";
            lblStatus.Text = "جارٍ المعالجة...";
        }
        else
        {
            huffman.Pause();
            btnPauseResume.Text = "استئناف";
            lblStatus.Text = "متوقف مؤقتاً...";
        }
        isPaused = !isPaused;
    }
    // نفس الأمر لخوارزمية فانو-شانون
}
```

### إلغاء العمليات

```csharp
private void btnCancel_Click(object sender, EventArgs e)
{
    if (cts != null && !cts.IsCancellationRequested)
    {
        // إذا كانت العملية متوقفة، استأنفها أولاً للاستجابة للإلغاء
        if (isPaused)
        {
            if (currentAlgorithm is HuffmanCompressor huffman)
                huffman.Resume();
            else if (currentAlgorithm is FanoShannonCompressor fano)
                fano.Resume();
            isPaused = false;
        }

        cts.Cancel();
        lblStatus.Text = "جارٍ إلغاء العملية...";
        btnCancel.Enabled = false;
        btnPauseResume.Enabled = false;
    }
}
```

---

## تكامل قائمة السياق (Context Menu)

### معالجة العمليات من قائمة السياق

```csharp
private async void Form1_Load(object sender, EventArgs e)
{
    // معالجة عمليات قائمة السياق
    if (!string.IsNullOrEmpty(contextMenuOperation) && !string.IsNullOrEmpty(contextMenuFilePath))
    {
        await HandleContextMenuOperation();
    }
}

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
        MessageBox.Show($"خطأ في العملية: {ex.Message}", "خطأ");
    }
}
```

### الضغط التلقائي للملفات

```csharp
private async Task AutoCompressFile(string filePath)
{
    // تحديد مسار الإخراج
    string outputPath = filePath + ".huff";

    // التحقق من وجود الملف المضغوط مسبقاً
    if (File.Exists(outputPath))
    {
        var result = MessageBox.Show(
            $"الملف المضغوط موجود مسبقاً:\n{outputPath}\n\nهل تريد استبداله؟",
            "تأكيد الاستبدال",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;
    }

    // عرض الواجهة الرئيسية وبدء الضغط
    lblStatus.Text = $"تم تحديد الملف للضغط: {Path.GetFileName(filePath)}";

    await StartOperationAsync(() =>
        currentAlgorithm.CompressAsync(filePath, outputPath, txtPassword.Text, UpdateProgress, cts.Token));
}
```

---

## خوارزمية هوفمان (HuffmanCompressor)

### بناء شجرة هوفمان

```csharp
private HuffmanNode BuildHuffmanTree(Dictionary<byte, int> frequencies)
{
    // إنشاء عقد أولية لكل رمز
    var nodes = frequencies.Select(p => new HuffmanNode
    {
        Symbol = p.Key,
        Frequency = p.Value
    }).ToList();

    // بناء الشجرة بدمج العقد
    while (nodes.Count > 1)
    {
        nodes.Sort((a, b) => a.Frequency.CompareTo(b.Frequency)); // ترتيب حسب التردد
        var left = nodes[0];   // العقدة ذات التردد الأقل
        var right = nodes[1];  // العقدة ذات التردد الثاني
        nodes.RemoveRange(0, 2);

        // إنشاء عقدة جديدة تجمع العقدتين
        nodes.Add(new HuffmanNode
        {
            Frequency = left.Frequency + right.Frequency,
            Left = left,
            Right = right
        });
    }

    return nodes[0]; // الجذر
}
```

### بناء جدول الأكواد

```csharp
private Dictionary<byte, string> BuildCodeTable(HuffmanNode root)
{
    var codes = new Dictionary<byte, string>();
    TraverseTree(root, "", codes);
    return codes;
}

private void TraverseTree(HuffmanNode node, string code, Dictionary<byte, string> codes)
{
    if (node == null) return;

    if (node.IsLeaf)
        codes[node.Symbol] = code; // حفظ الكود للرمز
    else
    {
        TraverseTree(node.Left, code + "0", codes);   // الفرع الأيسر = 0
        TraverseTree(node.Right, code + "1", codes);  // الفرع الأيمن = 1
    }
}
```

### عملية الضغط

```csharp
public async Task CompressAsync(string inputPath, string outputPath, string password = null,
                              Action<int> progressCallback = null, CancellationToken cancellationToken = default)
{
    await Task.Run(() =>
    {
        byte[] data = File.ReadAllBytes(inputPath);
        var frequencies = CalculateFrequencies(data);  // حساب الترددات
        var root = BuildHuffmanTree(frequencies);       // بناء الشجرة
        var codes = BuildCodeTable(root);               // بناء جدول الأكواد

        using (var fs = new FileStream(outputPath, FileMode.Create))
        using (var writer = new BinaryWriter(fs))
        {
            writer.Write("HUFFMAN");                    // توقيع الملف
            writer.Write(!string.IsNullOrEmpty(password)); // حماية بكلمة سر؟
            writer.Write(Path.GetFileName(inputPath));   // اسم الملف الأصلي
            writer.Write(data.Length);                   // حجم البيانات الأصلية
            writer.Write(frequencies.Count);             // عدد الترددات

            // كتابة جدول الترددات
            foreach (var pair in frequencies)
            {
                writer.Write(pair.Key);   // الرمز
                writer.Write(pair.Value); // التردد
            }

            // ضغط البيانات
            using (var compressedDataStream = new MemoryStream())
            using (var tempWriter = new BinaryWriter(compressedDataStream))
            {
                byte buffer = 0;
                int bufferLength = 0;

                foreach (byte b in data)
                {
                    CheckPauseState(cancellationToken); // فحص الإيقاف المؤقت

                    string code = codes[b]; // الحصول على كود الرمز
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

                // كتابة البتات المتبقية
                if (bufferLength > 0)
                {
                    buffer <<= (8 - bufferLength);
                    tempWriter.Write(buffer);
                }

                // تشفير البيانات إذا كانت محمية بكلمة سر
                byte[] compressedData = compressedDataStream.ToArray();
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

### عملية فك الضغط مع دعم تنسيقات متعددة

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

            // دعم كلاً من الملفات المفردة والمتعددة
            if (signature == "HUFFMAN_MULTI")
            {
                // أرشيف متعدد الملفات - استخراج الملف الأول
                fs.Position = 0; // العودة للبداية
                using (var reader2 = new BinaryReader(fs))
                {
                    reader2.ReadString(); // تخطي التوقيع
                    bool isPasswordProtected = reader2.ReadBoolean();

                    if (isPasswordProtected && string.IsNullOrEmpty(password))
                        throw new UnauthorizedAccessException("هذا الملف محمي بكلمة سر");

                    int fileCount = reader2.ReadInt32();
                    if (fileCount == 0)
                        throw new InvalidDataException("الأرشيف فارغ");

                    // استخراج الملف الأول
                    string fileName = reader2.ReadString();
                    int originalSize = reader2.ReadInt32();
                    int freqCount = reader2.ReadInt32();
                    var frequencies = new Dictionary<byte, int>();

                    for (int i = 0; i < freqCount; i++)
                        frequencies[reader2.ReadByte()] = reader2.ReadInt32();

                    // قراءة البيانات المضغوطة وفك تشفيرها إذا لزم الأمر
                    int compressedDataSize = reader2.ReadInt32();
                    byte[] compressedData = reader2.ReadBytes(compressedDataSize);

                    if (isPasswordProtected)
                    {
                        compressedData = PasswordProtection.Decrypt(compressedData, password);
                    }

                    // إعادة بناء الشجرة وفك الضغط
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
                    }

                    File.WriteAllBytes(outputPath, decompressedData);
                }
            }
            else if (signature == "HUFFMAN")
            {
                // ملف مفرد (التنسيق القديم)
                // ... نفس المنطق لفك ضغط الملفات المفردة
            }
            else
            {
                throw new InvalidDataException("هذا ليس ملف مضغوط باستخدام خوارزمية هوفمان");
            }
        }
    }, cancellationToken);
}
```

---

## خوارزمية فانو-شانون (FanoShannonCompressor)

### بناء أكواد شانون

```csharp
private Dictionary<byte, string> BuildShannonCodes(Dictionary<byte, int> frequencies)
{
    // ترتيب الرموز حسب التردد تنازلياً
    var nodes = frequencies.Select(p => new ShannonNode
    {
        Symbol = p.Key,
        Frequency = p.Value,
        Code = ""
    }).OrderByDescending(n => n.Frequency).ToList();

    // التعامل مع الحالات الخاصة
    if (nodes.Count == 0)
        return new Dictionary<byte, string>();

    if (nodes.Count == 1)
    {
        // رمز واحد فقط - تعيين كود بت واحد
        nodes[0].Code = "0";
        return nodes.ToDictionary(n => n.Symbol, n => n.Code);
    }

    BuildCodesRecursive(nodes, 0, nodes.Count - 1);
    return nodes.ToDictionary(n => n.Symbol, n => n.Code);
}
```

### البناء العودي للأكواد

```csharp
private void BuildCodesRecursive(List<ShannonNode> nodes, int start, int end)
{
    if (start >= end) return;

    // الحالة الأساسية: عقدتان
    if (end - start == 1)
    {
        nodes[start].Code += "0";
        nodes[end].Code += "1";
        return;
    }

    // العثور على نقطة التقسيم المثلى
    int totalFreq = 0;
    for (int i = start; i <= end; i++)
        totalFreq += nodes[i].Frequency;

    int currentSum = 0;
    int bestSplit = start;
    int bestDiff = int.MaxValue;

    // العثور على التقسيم الذي ينشئ التوزيع الأكثر توازناً
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

    // تعيين الأكواد: المجموعة اليسرى تحصل على "0"، اليمنى على "1"
    for (int i = start; i <= bestSplit; i++)
        nodes[i].Code += "0";

    for (int i = bestSplit + 1; i <= end; i++)
        nodes[i].Code += "1";

    // البناء العودي للمجموعتين
    BuildCodesRecursive(nodes, start, bestSplit);
    BuildCodesRecursive(nodes, bestSplit + 1, end);
}
```

---

## الحماية بكلمة السر (PasswordProtection)

### التشفير

```csharp
public static byte[] Encrypt(byte[] data, string password)
{
    using (var aes = Aes.Create())
    {
        // إنشاء salt عشوائي
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        // اشتقاق مفتاح من كلمة السر
        var key = DeriveKey(password, salt);

        // إنشاء IV عشوائي
        var iv = new byte[IvSize];
        RandomNumberGenerator.Fill(iv);

        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using (var encryptor = aes.CreateEncryptor())
        using (var msEncrypt = new MemoryStream())
        {
            // كتابة Salt و IV في المقدمة
            msEncrypt.Write(salt, 0, salt.Length);
            msEncrypt.Write(iv, 0, iv.Length);

            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                csEncrypt.Write(data, 0, data.Length);
            }

            return msEncrypt.ToArray();
        }
    }
}
```

### فك التشفير

```csharp
public static byte[] Decrypt(byte[] encryptedData, string password)
{
    using (var aes = Aes.Create())
    {
        // استخراج Salt من البداية
        var salt = new byte[SaltSize];
        Array.Copy(encryptedData, 0, salt, 0, SaltSize);

        // استخراج IV
        var iv = new byte[IvSize];
        Array.Copy(encryptedData, SaltSize, iv, 0, IvSize);

        // اشتقاق المفتاح
        var key = DeriveKey(password, salt);

        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // فك التشفير
        using (var decryptor = aes.CreateDecryptor())
        using (var msDecrypt = new MemoryStream(encryptedData, SaltSize + IvSize,
                                              encryptedData.Length - SaltSize - IvSize))
        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
        using (var result = new MemoryStream())
        {
            csDecrypt.CopyTo(result);
            return result.ToArray();
        }
    }
}

private static byte[] DeriveKey(string password, byte[] salt)
{
    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
    {
        return pbkdf2.GetBytes(KeySize); // 256-bit key
    }
}
```

---

## إدارة الإيقاف المؤقت والإلغاء

### فحص حالة الإيقاف المؤقت

```csharp
private void CheckPauseState(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested(); // فحص الإلغاء

    while (isPaused)
    {
        lock (pauseLock)
        {
            // فحص الإلغاء قبل الانتظار
            cancellationToken.ThrowIfCancellationRequested();

            // استخدام timeout للفحص الدوري للإلغاء
            Monitor.Wait(pauseLock, 500); // انتظار 500ms كحد أقصى

            // فحص الإلغاء بعد الانتظار
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
```

### آلية الإيقاف المؤقت

```csharp
public void Pause() => isPaused = true;

public void Resume()
{
    isPaused = false;
    lock (pauseLock)
    {
        Monitor.Pulse(pauseLock); // إشارة لاستئناف العمليات المتوقفة
    }
}
```

---

## تنسيقات الملفات

### توقيعات الملفات

- `"HUFFMAN"`: ملف مفرد بخوارزمية هوفمان
- `"HUFFMAN_MULTI"`: أرشيف متعدد الملفات بخوارزمية هوفمان
- `"FANO"`: ملف مفرد بخوارزمية فانو-شانون
- `"FANO_MULTI"`: أرشيف متعدد الملفات بخوارزمية فانو-شانون

### هيكل الملف المضغوط

```
[التوقيع (String)]
[محمي بكلمة سر (Boolean)]
[اسم الملف (String)]
[حجم البيانات الأصلية (Int32)]
[عدد الترددات (Int32)]
[جدول الترددات (Byte, Int32) * عدد الترددات]
[حجم البيانات المضغوطة (Int32)]
[البيانات المضغوطة (Byte[])]
```

### هيكل الأرشيف متعدد الملفات

```
[التوقيع (String)]
[محمي بكلمة سر (Boolean)]
[عدد الملفات (Int32)]
[
  لكل ملف:
  [اسم الملف (String)]
  [حجم البيانات الأصلية (Int32)]
  [عدد الترددات (Int32)]
  [جدول الترددات (Byte, Int32) * عدد الترددات]
  [حجم البيانات المضغوطة (Int32)]
  [البيانات المضغوطة (Byte[])]
]
```

---

## تكامل قائمة السياق في Windows

### ملف التركيب (install_context_menu.bat)

```batch
@echo off
echo Installing context menu integration...

set APP_PATH=%~dp0bin\Debug\app.exe

rem Add context menu for all files - Compress
reg add "HKEY_CLASSES_ROOT\*\shell\CompressFile" /ve /d "Compress File" /f
reg add "HKEY_CLASSES_ROOT\*\shell\CompressFile\command" /ve /d "\"%APP_PATH%\" /compress \"%%1\"" /f

rem Add context menu for compressed files - Extract
reg add "HKEY_CLASSES_ROOT\.huff\shell\ExtractFile" /ve /d "Extract File" /f
reg add "HKEY_CLASSES_ROOT\.huff\shell\ExtractFile\command" /ve /d "\"%APP_PATH%\" /decompress \"%%1\"" /f

reg add "HKEY_CLASSES_ROOT\.fs\shell\ExtractFile" /ve /d "Extract File" /f
reg add "HKEY_CLASSES_ROOT\.fs\shell\ExtractFile\command" /ve /d "\"%APP_PATH%\" /decompress \"%%1\"" /f

rem Add context menu for folders - Compress Folder
reg add "HKEY_CLASSES_ROOT\Directory\shell\CompressFolder" /ve /d "Compress Folder" /f
reg add "HKEY_CLASSES_ROOT\Directory\shell\CompressFolder\command" /ve /d "\"%APP_PATH%\" /compressfolder \"%%1\"" /f

rem Add context menu for empty space - Open File Compressor
reg add "HKEY_CLASSES_ROOT\Directory\Background\shell\OpenFileCompressor" /ve /d "Open File Compressor" /f
reg add "HKEY_CLASSES_ROOT\Directory\Background\shell\OpenFileCompressor\command" /ve /d "\"%APP_PATH%\"" /f

echo Context menu integration installed successfully!
pause
```

---

## نقطة دخول التطبيق (Program.cs)

```csharp
[STAThread]
static void Main(string[] args)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    if (args.Length >= 2)
    {
        // تشغيل من قائمة السياق
        string operation = args[0];
        string filePath = args[1];

        // التحقق من صحة المعاملات
        if (operation.StartsWith("/") || operation.StartsWith("-"))
        {
            if (File.Exists(filePath) || Directory.Exists(filePath))
            {
                Application.Run(new Form1(operation, filePath));
                return;
            }
            else
            {
                MessageBox.Show($"الملف أو المجلد غير موجود: {filePath}",
                              "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }

    // تشغيل عادي
    Application.Run(new Form1());
}
```

---

## الميزات المتقدمة

### مقارنة الخوارزميات

```csharp
private async void btnCompare_Click(object sender, EventArgs e)
{
    var ofd = new OpenFileDialog();
    if (ofd.ShowDialog() == DialogResult.OK)
    {
        var huffman = new HuffmanCompressor();
        var fano = new FanoShannonCompressor();

        try
        {
            // تقدير نسب الضغط لكلا الخوارزميتين
            var huffmanRatio = await huffman.EstimateCompressionRatioAsync(ofd.FileName);
            var fanoRatio = await fano.EstimateCompressionRatioAsync(ofd.FileName);

            MessageBox.Show($"نسبة الضغط المقدرة:\nهوفمان: {huffmanRatio:F2}%\nفانو-شانون: {fanoRatio:F2}%",
                          "مقارنة الخوارزميات", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في مقارنة الخوارزميات: {ex.Message}", "خطأ");
        }
    }
}
```

### تقدير نسبة الضغط

```csharp
public async Task<double> EstimateCompressionRatioAsync(string inputPath)
{
    return await Task.Run(() =>
    {
        // أخذ عينة 1MB من الملف للتقدير السريع
        byte[] sampleData = File.ReadAllBytes(inputPath).Take(1024 * 1024).ToArray();
        var frequencies = CalculateFrequencies(sampleData);
        var root = BuildHuffmanTree(frequencies);
        var codes = BuildCodeTable(root);

        double originalBits = sampleData.Length * 8;
        double compressedBits = sampleData.Sum(b => codes[b].Length);
        return (originalBits - compressedBits) / originalBits * 100;
    });
}
```

---

## إدارة الذاكرة والأداء

### المعالجة المتوازية للترددات

```csharp
private Dictionary<byte, int> CalculateFrequencies(byte[] data)
{
    return data.AsParallel()              // استخدام PLINQ للمعالجة المتوازية
              .GroupBy(b => b)            // تجميع البايتات المتشابهة
              .ToDictionary(g => g.Key, g => g.Count()); // إنشاء قاموس الترددات
}
```

### إدارة الذاكرة أثناء الضغط

```csharp
// استخدام MemoryStream مؤقت لتجنب استهلاك الذاكرة المفرط
using (var compressedDataStream = new MemoryStream())
using (var tempWriter = new BinaryWriter(compressedDataStream))
{
    // معالجة البيانات بالتدريج
    for (int i = 0; i < totalBytes; i++)
    {
        CheckPauseState(cancellationToken);

        // تحديث شريط التقدم كل 100 بايت
        if (i % 100 == 0)
        {
            int progress = (int)((i * 100.0) / totalBytes);
            progressCallback?.Invoke(progress);
        }
    }
}
```

---

## الخلاصة

هذا التطبيق يوفر:

1. **ضغط متقدم**: دعم خوارزميتي هوفمان وفانو-شانون
2. **حماية قوية**: تشفير AES-256 مع PBKDF2
3. **واجهة سهلة**: تكامل مع قائمة السياق في Windows
4. **أداء عالي**: معالجة متوازية وإدارة ذكية للذاكرة
5. **مرونة**: دعم الملفات المفردة والمتعددة والمجلدات
6. **تحكم كامل**: إيقاف مؤقت وإلغاء وتتبع التقدم

التطبيق مصمم ليكون أداة شاملة وفعالة لضغط الملفات مع التركيز على الأمان والسهولة في الاستخدام.
