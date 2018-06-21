using AquaPlusEditor;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace APEGUI {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }


        CSTS ScriptEditor;
        private void extractToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "All *.pak packgets|*.pak;*.dat;*.sdat";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            Stream Packget = new StreamReader(fd.FileName).BaseStream;
            string Outdir = fd.FileName + "~\\";
            if (Directory.Exists(Outdir))
                Directory.Delete(Outdir, true);
            Directory.CreateDirectory(Outdir);

            Entry[] Entries = PAK.Open(Packget);

            foreach (var File in Entries) {
                string OP = Outdir + File.Filename;
                Stream Output = new StreamWriter(OP).BaseStream;
                File.Content.CopyTo(Output);
                Output.Flush();
                Output.Close();
            }
            MessageBox.Show("Packget extracted");
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e) {
            try {
                textBox1.Text = listBox1.Items[listBox1.SelectedIndex].ToString();
                Text = "id: " + listBox1.SelectedIndex;
            } catch { }
        }
        private void textBox1_KeyPress(object sender, KeyPressEventArgs e) {
            if (e.KeyChar == '\n' || e.KeyChar == '\r') {
                try {
                    listBox1.Items[listBox1.SelectedIndex] = textBox1.Text;
                } catch {

                }
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "All Script Files|*.bin";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            byte[] Script = File.ReadAllBytes(fd.FileName);

            ScriptEditor = new CSTS(Script);
            string[] Strings = ScriptEditor.Import();

            listBox1.Items.Clear();
            foreach (string str in Strings)
                listBox1.Items.Add(str);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            SaveFileDialog fd = new SaveFileDialog();
            fd.Filter = "All Script Files|*.bin";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            string[] Strings = listBox1.Items.Cast<string>().ToArray();
            byte[] Script = ScriptEditor.Export(Strings);
            File.WriteAllBytes(fd.FileName, Script);

            MessageBox.Show("Script Saved");
        }

        private void repackToolStripMenuItem_Click(object sender, EventArgs e) {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = "Folder to pack";
            if (fbd.ShowDialog() != DialogResult.OK)
                return;

            SaveFileDialog fd = new SaveFileDialog();
            fd.Title = "Save as...";
            fd.Filter = "All *.pak packgets|*.pak;*.dat;*.sdat";

            if (fd.ShowDialog() != DialogResult.OK)
                return;

            bool BigEnddian = MessageBox.Show("Pack with BigEnddian?\nYes: PS3 Format\nNo: PSV/PS4 Format", "APEGUI", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            if (!fbd.SelectedPath.EndsWith("\\"))
                fbd.SelectedPath += '\\';

            string[] Files = Directory.GetFiles(fbd.SelectedPath, "*.*");

            Entry[] Entries = (from x in Files
                               select new Entry() {
                                   Filename = x.Substring(fbd.SelectedPath.Length),
                                   Content = new StreamReader(x).BaseStream
                               }).ToArray();

            Stream Output = new StreamWriter(fd.FileName).BaseStream;

            PAK.Save(Output, Entries, BigEnddian);
            MessageBox.Show("Packget Saved");
        }
    }
}
