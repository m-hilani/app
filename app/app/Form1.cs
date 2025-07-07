using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileCompressorApp
{
    public partial class Form1 : Form
    {
        private ICompressionAlgorithm currentAlgorithm;
        private CancellationTokenSource cts;
        private bool isPaused = false;
        private string contextMenuFilePath;
        private string contextMenuOperation;

        public Form1()
        {
            InitializeComponent();
            currentAlgorithm = new HuffmanCompressor(); // افتراضيًا هوفمان
        }

        public Form1(string operation, string filePath) : this()
        {
            contextMenuOperation = operation;
            contextMenuFilePath = filePath;
        }

        private async void btnMultiCompress_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var sfd = new SaveFileDialog();
                sfd.Filter = currentAlgorithm is HuffmanCompressor ? "ملف Huffman مضغوط|*.huff" : "ملف Fano-Shannon مضغوط|*.fs";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var files = ofd.FileNames.ToDictionary(f => f, f => Path.GetFileName(f));
                    await StartOperationAsync(() =>
                        currentAlgorithm.CompressMultipleAsync(files, sfd.FileName, txtPassword.Text, UpdateProgress, cts.Token));
                }
            }
        }

        private async void btnExtractSingle_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "ملفات مضغوطة|*.huff;*.fs";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Auto-detect the algorithm based on file extension
                    ICompressionAlgorithm extractAlgorithm;
                    if (ofd.FileName.EndsWith(".huff", StringComparison.OrdinalIgnoreCase))
                    {
                        extractAlgorithm = new HuffmanCompressor();
                        radioHuffman.Checked = true;
                    }
                    else if (ofd.FileName.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
                    {
                        extractAlgorithm = new FanoShannonCompressor();
                        radioFanoShannon.Checked = true;
                    }
                    else
                    {
                        MessageBox.Show("نوع الملف المضغوط غير مدعوم. يجب أن يكون .huff أو .fs", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Get the list of files in the archive
                    var fileList = await extractAlgorithm.ListFilesInArchiveAsync(ofd.FileName, txtPassword.Text);
                    
                    if (fileList.Count == 0)
                    {
                        MessageBox.Show("لا توجد ملفات في الأرشيف!", "تحذير", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var inputForm = new ExtractSingleFileForm();
                    inputForm.SetAvailableFiles(fileList);
                    
                    if (inputForm.ShowDialog() == DialogResult.OK)
                    {
                        if (string.IsNullOrEmpty(inputForm.FileNameToExtract))
                        {
                            MessageBox.Show("يرجى اختيار ملف للاستخراج!", "تحذير", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        var sfd = new SaveFileDialog();
                        sfd.FileName = inputForm.FileNameToExtract;
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            try
                            {
                                await StartOperationAsync(() =>
                                    extractAlgorithm.ExtractSingleFileAsync(ofd.FileName, inputForm.FileNameToExtract,
                                                                          sfd.FileName, txtPassword.Text, UpdateProgress, cts.Token));
                                MessageBox.Show($"تم استخراج الملف بنجاح!", "نجاح",
                                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (FileNotFoundException ex)
                            {
                                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في قراءة الأرشيف: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        private async void btnDecompress_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "ملفات مضغوطة|*.huff;*.fs";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                // Auto-detect the algorithm based on file extension
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
                    MessageBox.Show("نوع الملف المضغوط غير مدعوم. يجب أن يكون .huff أو .fs", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var sfd = new SaveFileDialog();
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await StartOperationAsync(() =>
                        decompressAlgorithm.DecompressAsync(ofd.FileName, sfd.FileName, txtPassword.Text, UpdateProgress, cts.Token));
                }
            }
        }

        private async Task StartOperationAsync(Func<Task> operation)
        {
            try
            {
                // Disable all operation buttons and enable control buttons
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

        private void DisableOperationButtons()
        {
            btnDecompress.Enabled = false;
            btnMultiCompress.Enabled = false;
            btnExtractSingle.Enabled = false;
            btnCompressFolder.Enabled = false;
            btnCompare.Enabled = false;
        }

        private void EnableOperationButtons()
        {
            btnDecompress.Enabled = true;
            btnMultiCompress.Enabled = true;
            btnExtractSingle.Enabled = true;
            btnCompressFolder.Enabled = true;
            btnCompare.Enabled = true;
        }

        private void UpdateProgress(int progress)
        {
            this.Invoke((MethodInvoker)(() =>
            {
                progressBar1.Value = progress;
                lblStatus.Text = $"جارٍ المعالجة... {progress}%";
            }));
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                // If the operation is paused, resume it first so it can properly respond to cancellation
                if (isPaused)
                {
                    if (currentAlgorithm is HuffmanCompressor huffman)
                    {
                        huffman.Resume();
                    }
                    else if (currentAlgorithm is FanoShannonCompressor fano)
                    {
                        fano.Resume();
                    }
                    isPaused = false;
                }
                
                cts.Cancel();
                lblStatus.Text = "جارٍ إلغاء العملية...";
                btnCancel.Enabled = false;
                btnPauseResume.Enabled = false;
            }
        }

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
            else if (currentAlgorithm is FanoShannonCompressor fano)
            {
                if (isPaused)
                {
                    fano.Resume();
                    btnPauseResume.Text = "إيقاف مؤقت";
                    lblStatus.Text = "جارٍ المعالجة...";
                }
                else
                {
                    fano.Pause();
                    btnPauseResume.Text = "استئناف";
                    lblStatus.Text = "متوقف مؤقتاً...";
                }
                isPaused = !isPaused;
            }
        }

        private void ResetUI()
        {
            progressBar1.Value = 0;
            lblStatus.Text = "جاهز";
            
            // Reset pause state in algorithms
            if (isPaused)
            {
                if (currentAlgorithm is HuffmanCompressor huffman)
                {
                    huffman.Resume();
                }
                else if (currentAlgorithm is FanoShannonCompressor fano)
                {
                    fano.Resume();
                }
            }
            
            // Re-enable operation buttons
            EnableOperationButtons();
            
            // Disable control buttons and reset their state
            btnPauseResume.Enabled = false;
            btnCancel.Enabled = false;
            btnPauseResume.Text = "إيقاف مؤقت";
            isPaused = false;
        }

        private void radioHuffman_CheckedChanged(object sender, EventArgs e)
        {
            if (radioHuffman.Checked) currentAlgorithm = new HuffmanCompressor();
        }

        private void radioFanoShannon_CheckedChanged(object sender, EventArgs e)
        {
            if (radioFanoShannon.Checked) currentAlgorithm = new FanoShannonCompressor();
        }

        private async void btnCompare_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var huffman = new HuffmanCompressor();
                var fano = new FanoShannonCompressor();

                var huffmanRatio = await huffman.EstimateCompressionRatioAsync(ofd.FileName);
                var fanoRatio = await fano.EstimateCompressionRatioAsync(ofd.FileName);

                MessageBox.Show($"نسبة الضغط المقدرة:\nهوفمان: {huffmanRatio:F2}%\nفانو-شانون: {fanoRatio:F2}%",
                              "مقارنة الخوارزميات", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // Handle context menu operations
            if (!string.IsNullOrEmpty(contextMenuOperation) && !string.IsNullOrEmpty(contextMenuFilePath))
            {
                await HandleContextMenuOperation();
            }

            // Initialize multi-threading status
            UpdateMultiThreadingStatus();
        }

        private void UpdateMultiThreadingStatus()
        {
            this.Text = $"File Compressor - Multi-Threading: {CompressionConfig.MaxThreads} threads";
        }

        private void btnConfigureThreads_Click(object sender, EventArgs e)
        {
            var form = new ThreadConfigForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                UpdateMultiThreadingStatus();
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
                MessageBox.Show($"خطأ في العملية: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AutoCompressFile(string filePath)
        {
            // Set the output path
            string outputPath = filePath + ".huff";
            
            // Check if output file already exists
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

            // Show the main UI and start compression
            lblStatus.Text = $"تم تحديد الملف للضغط: {Path.GetFileName(filePath)}";
            
            await StartOperationAsync(() =>
                currentAlgorithm.CompressAsync(filePath, outputPath, txtPassword.Text, UpdateProgress, cts.Token));
        }

        private async Task AutoDecompressFile(string filePath)
        {
            // Determine the algorithm based on file extension
            if (filePath.EndsWith(".huff", StringComparison.OrdinalIgnoreCase))
            {
                currentAlgorithm = new HuffmanCompressor();
            }
            else if (filePath.EndsWith(".fs", StringComparison.OrdinalIgnoreCase))
            {
                currentAlgorithm = new FanoShannonCompressor();
                radioFanoShannon.Checked = true;
            }
            else
            {
                MessageBox.Show("نوع الملف المضغوط غير مدعوم. يجب أن يكون .huff أو .fs", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string outputPath = filePath.Substring(0, filePath.LastIndexOf('.'));
            
            // Check if output file already exists
            if (File.Exists(outputPath))
            {
                var result = MessageBox.Show(
                    $"الملف المفكوك موجود مسبقاً:\n{outputPath}\n\nهل تريد استبداله؟",
                    "تأكيد الاستبدال",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                
                if (result != DialogResult.Yes)
                    return;
            }

            // Show the main UI and start decompression
            lblStatus.Text = $"تم تحديد الملف لفك الضغط: {Path.GetFileName(filePath)}";
            
            await StartOperationAsync(() =>
                currentAlgorithm.DecompressAsync(filePath, outputPath, txtPassword.Text, UpdateProgress, cts.Token));
        }

        private async Task AutoCompressFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("المجلد غير موجود: " + folderPath, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Get all files in folder
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                MessageBox.Show("لا توجد ملفات في المجلد للضغط!", "تحذير", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var fileDictionary = new Dictionary<string, string>();
            foreach (var file in files)
            {
                string relativePath = GetRelativePath(folderPath, file);
                fileDictionary[file] = relativePath;
            }

            string outputPath = folderPath + ".huff";
            
            // Check if output file already exists
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

            // Show the main UI and start compression
            lblStatus.Text = $"تم تحديد المجلد للضغط: {Path.GetFileName(folderPath)} ({files.Length} ملف)";
            
            await StartOperationAsync(() =>
                currentAlgorithm.CompressMultipleAsync(fileDictionary, outputPath, txtPassword.Text, UpdateProgress, cts.Token));
        }

        private async void btnCompressFolder_Click(object sender, EventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            fbd.Description = "اختر المجلد المراد ضغط ملفاته";
            
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Get all files from the selected folder
                    var allFiles = Directory.GetFiles(fbd.SelectedPath, "*", SearchOption.AllDirectories);
                    
                    if (allFiles.Length == 0)
                    {
                        MessageBox.Show("لا توجد ملفات في المجلد المختار!", "تحذير", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Ask user about subfolder inclusion
                    var result = MessageBox.Show("هل تريد تضمين الملفات من المجلدات الفرعية؟", 
                                                "تأكيد", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Cancel)
                        return;

                    string[] filesToCompress;
                    if (result == DialogResult.Yes)
                    {
                        // Include all files from subdirectories
                        filesToCompress = Directory.GetFiles(fbd.SelectedPath, "*", SearchOption.AllDirectories);
                    }
                    else
                    {
                        // Only files from the main directory
                        filesToCompress = Directory.GetFiles(fbd.SelectedPath, "*", SearchOption.TopDirectoryOnly);
                    }

                    if (filesToCompress.Length == 0)
                    {
                        MessageBox.Show("لا توجد ملفات للضغط!", "تحذير", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Show info about files to be compressed
                    var confirmMessage = $"سيتم ضغط {filesToCompress.Length} ملف(ات) من المجلد:\n{fbd.SelectedPath}\n\nهل تريد المتابعة؟";
                    if (MessageBox.Show(confirmMessage, "تأكيد الضغط", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;

                    // Choose output file
                    var sfd = new SaveFileDialog();
                    sfd.Filter = currentAlgorithm is HuffmanCompressor ? "ملف Huffman مضغوط|*.huff" : "ملف Fano-Shannon مضغوط|*.fs";
                    sfd.FileName = $"{new DirectoryInfo(fbd.SelectedPath).Name}_compressed";
                    
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        // Create dictionary with full paths as keys and relative paths as values
                        var files = new Dictionary<string, string>();
                        var basePath = fbd.SelectedPath;
                        
                        foreach (var file in filesToCompress)
                        {
                            // Create relative path for storage in archive
                            var relativePath = GetRelativePath(basePath, file);
                            files[file] = relativePath;
                        }

                        await StartOperationAsync(() =>
                            currentAlgorithm.CompressMultipleAsync(files, sfd.FileName, txtPassword.Text, UpdateProgress, cts.Token));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في الوصول للمجلد: {ex.Message}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            // Helper method to create relative paths compatible with .NET Framework 4.7.2
            Uri baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);
            
            if (baseUri.Scheme != fullUri.Scheme)
                return fullPath; // Cannot make relative
                
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        
    }

    // نموذج جديد لاختيار الملف المراد استخراجه
    public class ExtractSingleFileForm : Form
    {
        private ComboBox cmbFileName;
        private Button btnOk;
        private Button btnCancel;
        private Label lblInstruction;

        public string FileNameToExtract => cmbFileName.SelectedItem?.ToString() ?? "";

        public ExtractSingleFileForm()
        {
            InitializeComponent();
            this.Text = "اختر الملف المراد استخراجه";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }

        public void SetAvailableFiles(List<string> fileNames)
        {
            cmbFileName.Items.Clear();
            foreach (var fileName in fileNames)
            {
                cmbFileName.Items.Add(fileName);
            }
            if (cmbFileName.Items.Count > 0)
                cmbFileName.SelectedIndex = 0;
        }

        private void InitializeComponent()
        {
            this.lblInstruction = new Label();
            this.cmbFileName = new ComboBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.SuspendLayout();

            // lblInstruction
            this.lblInstruction.AutoSize = true;
            this.lblInstruction.Location = new System.Drawing.Point(12, 15);
            this.lblInstruction.Name = "lblInstruction";
            this.lblInstruction.Size = new System.Drawing.Size(200, 17);
            this.lblInstruction.Text = "اختر الملف الذي تريد استخراجه:";

            // cmbFileName
            this.cmbFileName.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbFileName.FormattingEnabled = true;
            this.cmbFileName.Location = new System.Drawing.Point(12, 40);
            this.cmbFileName.Name = "cmbFileName";
            this.cmbFileName.Size = new System.Drawing.Size(350, 24);
            this.cmbFileName.TabIndex = 1;

            // btnOk
            this.btnOk.DialogResult = DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(200, 80);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 30);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "موافق";
            this.btnOk.UseVisualStyleBackColor = true;

            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(290, 80);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "إلغاء";
            this.btnCancel.UseVisualStyleBackColor = true;

            // ExtractSingleFileForm
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(380, 130);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.cmbFileName);
            this.Controls.Add(this.lblInstruction);
            this.Name = "ExtractSingleFileForm";
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }

    // نموذج تكوين المعالجة المتوازية
    public class ThreadConfigForm : Form
    {
        private Label lblMaxThreads;
        private NumericUpDown numMaxThreads;
        private Label lblChunkSize;
        private NumericUpDown numChunkSize;
        private Label lblMinFileSize;
        private NumericUpDown numMinFileSize;
        private Button btnOk;
        private Button btnCancel;
        private Button btnReset;
        private Label lblCurrentCpus;

        public ThreadConfigForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            numMaxThreads.Value = CompressionConfig.MaxThreads;
            numChunkSize.Value = CompressionConfig.ChunkSizeBytes / (1024 * 1024); // Convert to MB
            numMinFileSize.Value = CompressionConfig.MinFileSizeForChunking / (1024 * 1024); // Convert to MB
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            CompressionConfig.MaxThreads = (int)numMaxThreads.Value;
            CompressionConfig.ChunkSizeBytes = (int)numChunkSize.Value * 1024 * 1024; // Convert from MB
            CompressionConfig.MinFileSizeForChunking = (int)numMinFileSize.Value * 1024 * 1024; // Convert from MB
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            numMaxThreads.Value = Environment.ProcessorCount;
            numChunkSize.Value = 1; // 1MB
            numMinFileSize.Value = 5; // 5MB
        }

        private void InitializeComponent()
        {
            this.lblMaxThreads = new Label();
            this.numMaxThreads = new NumericUpDown();
            this.lblChunkSize = new Label();
            this.numChunkSize = new NumericUpDown();
            this.lblMinFileSize = new Label();
            this.numMinFileSize = new NumericUpDown();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.btnReset = new Button();
            this.lblCurrentCpus = new Label();
            ((System.ComponentModel.ISupportInitialize)(this.numMaxThreads)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChunkSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMinFileSize)).BeginInit();
            this.SuspendLayout();

            // lblCurrentCpus
            this.lblCurrentCpus.AutoSize = true;
            this.lblCurrentCpus.Location = new System.Drawing.Point(12, 15);
            this.lblCurrentCpus.Name = "lblCurrentCpus";
            this.lblCurrentCpus.Size = new System.Drawing.Size(200, 17);
            this.lblCurrentCpus.Text = $"Available CPU Cores: {Environment.ProcessorCount}";

            // lblMaxThreads
            this.lblMaxThreads.AutoSize = true;
            this.lblMaxThreads.Location = new System.Drawing.Point(12, 45);
            this.lblMaxThreads.Name = "lblMaxThreads";
            this.lblMaxThreads.Size = new System.Drawing.Size(150, 17);
            this.lblMaxThreads.Text = "Maximum Threads:";

            // numMaxThreads
            this.numMaxThreads.Location = new System.Drawing.Point(180, 43);
            this.numMaxThreads.Minimum = 1;
            this.numMaxThreads.Maximum = Environment.ProcessorCount * 2;
            this.numMaxThreads.Name = "numMaxThreads";
            this.numMaxThreads.Size = new System.Drawing.Size(80, 22);
            this.numMaxThreads.TabIndex = 1;

            // lblChunkSize
            this.lblChunkSize.AutoSize = true;
            this.lblChunkSize.Location = new System.Drawing.Point(12, 75);
            this.lblChunkSize.Name = "lblChunkSize";
            this.lblChunkSize.Size = new System.Drawing.Size(150, 17);
            this.lblChunkSize.Text = "Chunk Size (MB):";

            // numChunkSize
            this.numChunkSize.Location = new System.Drawing.Point(180, 73);
            this.numChunkSize.Minimum = 1;
            this.numChunkSize.Maximum = 100;
            this.numChunkSize.Name = "numChunkSize";
            this.numChunkSize.Size = new System.Drawing.Size(80, 22);
            this.numChunkSize.TabIndex = 2;

            // lblMinFileSize
            this.lblMinFileSize.AutoSize = true;
            this.lblMinFileSize.Location = new System.Drawing.Point(12, 105);
            this.lblMinFileSize.Name = "lblMinFileSize";
            this.lblMinFileSize.Size = new System.Drawing.Size(160, 17);
            this.lblMinFileSize.Text = "Min File Size for Parallel (MB):";

            // numMinFileSize
            this.numMinFileSize.Location = new System.Drawing.Point(180, 103);
            this.numMinFileSize.Minimum = 1;
            this.numMinFileSize.Maximum = 1000;
            this.numMinFileSize.Name = "numMinFileSize";
            this.numMinFileSize.Size = new System.Drawing.Size(80, 22);
            this.numMinFileSize.TabIndex = 3;

            // btnReset
            this.btnReset.Location = new System.Drawing.Point(12, 140);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 30);
            this.btnReset.TabIndex = 4;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);

            // btnOk
            this.btnOk.Location = new System.Drawing.Point(110, 140);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 30);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);

            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(200, 140);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 30);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;

            // ThreadConfigForm
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(290, 190);
            this.Controls.Add(this.lblCurrentCpus);
            this.Controls.Add(this.lblMaxThreads);
            this.Controls.Add(this.numMaxThreads);
            this.Controls.Add(this.lblChunkSize);
            this.Controls.Add(this.numChunkSize);
            this.Controls.Add(this.lblMinFileSize);
            this.Controls.Add(this.numMinFileSize);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ThreadConfigForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Multi-Threading Configuration";
            ((System.ComponentModel.ISupportInitialize)(this.numMaxThreads)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChunkSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMinFileSize)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
