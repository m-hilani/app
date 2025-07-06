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

        public Form1()
        {
            InitializeComponent();
            currentAlgorithm = new HuffmanCompressor(); // افتراضيًا هوفمان
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
                        currentAlgorithm.CompressMultipleAsync(files, sfd.FileName, txtPassword.Text, UpdateProgress));
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
                    // Get the list of files in the archive
                    var fileList = await currentAlgorithm.ListFilesInArchiveAsync(ofd.FileName, txtPassword.Text);
                    
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
                                    currentAlgorithm.ExtractSingleFileAsync(ofd.FileName, inputForm.FileNameToExtract,
                                                                          sfd.FileName, txtPassword.Text, UpdateProgress));
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

        private async void btnCompress_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var sfd = new SaveFileDialog();
                sfd.Filter = currentAlgorithm is HuffmanCompressor ? "ملف Huffman مضغوط|*.huff" : "ملف Fano-Shannon مضغوط|*.fs";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await StartOperationAsync(() =>
                        currentAlgorithm.CompressAsync(ofd.FileName, sfd.FileName, txtPassword.Text, UpdateProgress));
                }
            }
        }

        private async void btnDecompress_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "ملفات مضغوطة|*.huff;*.fs";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var sfd = new SaveFileDialog();
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    await StartOperationAsync(() =>
                        currentAlgorithm.DecompressAsync(ofd.FileName, sfd.FileName, txtPassword.Text, UpdateProgress));
                }
            }
        }

        private async Task StartOperationAsync(Func<Task> operation)
        {
            try
            {
              
                btnPauseResume.Enabled = true;
                cts = new CancellationTokenSource();

                await operation();

                MessageBox.Show("تمت العملية بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("تم إلغاء العملية", "معلومة", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void UpdateProgress(int progress)
        {
            this.Invoke((MethodInvoker)(() =>
            {
                progressBar1.Value = progress;
                lblStatus.Text = $"جارٍ المعالجة... {progress}%";
            }));
        }

        //private void btnCancel_Click(object sender, EventArgs e)
        //{
        //    cts?.Cancel();
        //}

        private void btnPauseResume_Click(object sender, EventArgs e)
        {
            if (currentAlgorithm is HuffmanCompressor huffman)
            {
                if (isPaused)
                {
                    huffman.Resume();
                    btnPauseResume.Text = "إيقاف مؤقت";
                }
                else
                {
                    huffman.Pause();
                    btnPauseResume.Text = "استئناف";
                }
                isPaused = !isPaused;
            }
        }

        private void ResetUI()
        {
            progressBar1.Value = 0;
            lblStatus.Text = "جاهز";
          
            btnPauseResume.Enabled = false;
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

        private void Form1_Load(object sender, EventArgs e)
        {
            // يمكن إضافة أي تهيئة إضافية هنا
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
                            currentAlgorithm.CompressMultipleAsync(files, sfd.FileName, txtPassword.Text, UpdateProgress));
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
        }

        public void SetAvailableFiles(List<string> fileNames)
        {
            cmbFileName.Items.Clear();
            cmbFileName.Items.AddRange(fileNames.ToArray());
            if (fileNames.Count > 0)
                cmbFileName.SelectedIndex = 0;
        }

        private void InitializeComponent()
        {
            this.lblInstruction = new Label();
            this.cmbFileName = new ComboBox();
            this.btnOk = new Button { Text = "موافق", DialogResult = DialogResult.OK };
            this.btnCancel = new Button { Text = "إلغاء", DialogResult = DialogResult.Cancel };

            this.lblInstruction.Text = "اختر الملف المراد استخراجه:";
            this.lblInstruction.Location = new System.Drawing.Point(20, 10);
            this.lblInstruction.Size = new System.Drawing.Size(200, 20);

            this.cmbFileName.Location = new System.Drawing.Point(20, 35);
            this.cmbFileName.Size = new System.Drawing.Size(200, 20);
            this.cmbFileName.DropDownStyle = ComboBoxStyle.DropDownList;

            this.btnOk.Location = new System.Drawing.Point(20, 70);
            this.btnCancel.Location = new System.Drawing.Point(120, 70);

            this.ClientSize = new System.Drawing.Size(240, 110);
            this.Controls.AddRange(new Control[] { lblInstruction, cmbFileName, btnOk, btnCancel });

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Text = "استخراج ملف من الأرشيف";
        }
    }
}
