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
        private Button btnMultiCompress;
        private Button btnExtractSingle;

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
                var inputForm = new ExtractSingleFileForm();
                if (inputForm.ShowDialog() == DialogResult.OK)
                {
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

        
    }

    // نموذج جديد لإدخال اسم الملف المراد استخراجه
    public class ExtractSingleFileForm : Form
    {
        private TextBox txtFileName;
        private Button btnOk;
        private Button btnCancel;

        public string FileNameToExtract => txtFileName.Text;

        public ExtractSingleFileForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.txtFileName = new TextBox();
            this.btnOk = new Button { Text = "موافق", DialogResult = DialogResult.OK };
            this.btnCancel = new Button { Text = "إلغاء", DialogResult = DialogResult.Cancel };

            this.txtFileName.Location = new System.Drawing.Point(20, 20);
            this.txtFileName.Size = new System.Drawing.Size(200, 20);

            this.btnOk.Location = new System.Drawing.Point(20, 50);
            this.btnCancel.Location = new System.Drawing.Point(120, 50);

            this.ClientSize = new System.Drawing.Size(240, 100);
            this.Controls.AddRange(new Control[] { txtFileName, btnOk, btnCancel });

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
            this.Text = "إدخال اسم الملف";
        }
    }
}
