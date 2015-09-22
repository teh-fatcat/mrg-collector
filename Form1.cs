using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Mrg_Collector {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            DialogResult dr = openFileDialog1.ShowDialog();

            if (dr == DialogResult.OK) {
                for (byte i = 0; i < openFileDialog1.FileNames.Length; i++) {
                    string item = openFileDialog1.FileNames[i];
                    listBox1.Items.Add(item);
                    UpdateCounters(item);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            for (int i = listBox1.SelectedIndices.Count - 1; i >= 0; i--) {
                object curid = listBox1.SelectedItems[i];
                string item = listBox1.GetItemText(curid);
                listBox1.Items.Remove(curid);
                UpdateCounters(item, true);
            }
            listBox1.ClearSelected();
        }

        private void button3_Click(object sender, EventArgs e) {

            DialogResult sf = saveFileDialog1.ShowDialog();

            button1.Enabled = false;        //РУКИ ПРОЧЬ!
            button2.Enabled = false;
            button3.Enabled = false;
            progressBar1.Value = 0;
            Cursor.Current = Cursors.WaitCursor;

            if (sf == DialogResult.OK) {
                using (FileStream fs = new FileStream(saveFileDialog1.FileName, FileMode.Create, FileAccess.Write)) {
                    using (BinaryWriter w = new BinaryWriter(fs)) {
                        List<uint>[] list = new List<uint>[3]; //массив списков смещений чтения заголовка
                        List<List<Int32>> trackBytes = new List<List<Int32>>(); //список списков стартовых байтов отдельных треков
                        List<List<uint>> newBytes = new List<List<uint>>(); //список адресов треков в заголовке
                        List<UInt16> tracksCount = new List<ushort>();
                        for (byte c = 0; c < 3; c++) {
                            list[c] = new List<uint>();
                            int cc = 0;
                            w.Write((uint)ReverseByteOrder(Counters.getById(c)));
                            foreach (object item in listBox1.Items) {
                                using (FileStream fso = new FileStream(listBox1.GetItemText(item), FileMode.Open, FileAccess.Read)) {
                                    using (BinaryReader r = new BinaryReader(fso)) {
                                        if (c != 0) {
                                            fso.Position = list[c - 1][cc];
                                        } else {
                                            tracksCount.Add(0);
                                            trackBytes.Add(new List<int>());
                                            newBytes.Add(new List<uint>());
                                        }
                                        ushort num = (UInt16)ReverseByteOrder(r.ReadInt32());      //количество треков на c-том левеле item-ого файла
                                        tracksCount[cc] += num;
                                        byte b;
                                        uint cp = 0;
                                        for (uint i = 0; i < num; i++) {    //UINT16.MAXVALUE хватит всем!
                                            trackBytes[cc].Add(ReverseByteOrder(r.ReadInt32()));      //запомнить позицию трека в исходном файле
                                            cp = (uint)fs.Position;
                                            newBytes[cc].Add(cp);        //запомнить позицию трека в заголовке
                                            w.Write(new byte[] { 0x01, 0x01, 0x01, 0x01 });
                                            do {
                                                b = r.ReadByte();
                                                w.Write(b);
                                            } while (b != 0);
                                        }
                                        list[c].Add((uint)fso.Position);
                                        if (c == 2) {
                                            trackBytes[cc].Add((Int32)fso.Length);
                                            newBytes[cc].Add(cp);
                                        }
                                        cc++;
                                        progressBar1.Value = (int)Math.Round((double)((c + 1) * (cc) / (3 * listBox1.Items.Count) * progressBar1.Maximum / 2));
                                    }
                                }
                            }
                        }
                        //начать писать треки
                        UInt16 k = 0;
                        foreach (object item in listBox1.Items) {
                            using (FileStream fso = new FileStream(listBox1.GetItemText(item), FileMode.Open, FileAccess.Read)) {
                                using (BinaryReader r = new BinaryReader(fso)) {
                                    for (ushort i = 0; i < tracksCount[k]; i++) {
                                        int cp = (int)fs.Position;
                                        fs.Position = newBytes[k][i];
                                        w.Write((uint)ReverseByteOrder(cp));
                                        fs.Position = cp;
                                        fso.Position = trackBytes[k][i];
                                        while (fso.Position != trackBytes[k][i + 1]) { //при обходе первой сложности натыкается на начало второго файла. поправить.
                                            w.Write(r.ReadByte());
                                        }
                                    }
                                }
                            }
                            k++;
                            progressBar1.Value = (int)Math.Round((double)(progressBar1.Maximum / 2 * (1 + k / listBox1.Items.Count)));
                        }
                    }
                }
                MessageBox.Show("File \"" + Reverse(saveFileDialog1.FileName.Split('\\')).GetValue(0) + "\" was successfully created", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Cursor.Current = Cursors.Default;
            button1.Enabled = true;
            //button2.Enabled = true;
            button3.Enabled = true;     //ТЕПЕРЬ ЕЩЁ РАЗОК
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start("http://dinedi.net/go.php?http://dinedi.ru/");
        }

        private void UpdateCounters(String mrgDir, bool dec = false) {
            using (FileStream fs = new FileStream(mrgDir, FileMode.Open, FileAccess.Read)) {
                using (BinaryReader r = new BinaryReader(fs)) {
                    byte b = 0, c = 0;
                    ushort[] lc = new ushort[3];
                    while (c != 3) {
                        ushort num = Convert.ToUInt16(ReverseByteOrder(r.ReadInt32()));
                        lc[c] = num;
                        for (ushort i = 0; i < num; i++) {
                            r.ReadInt32();
                            do b = r.ReadByte(); while (b != 0);
                        }
                        c++;
                    }
                    if (dec) Counters.setCounters(this, -lc[0], -lc[1], -lc[2], -1);    //ГЫГЫ
                    else Counters.setCounters(this, lc[0], lc[1], lc[2], 1);
                    button3.Enabled = (listBox1.Items.Count == 0) ? false : true;
                }
            }
        }

        public Int32 ReverseByteOrder(Int32 le) {
            byte[] numb = BitConverter.GetBytes(le);
            Array.Reverse(numb);
            return BitConverter.ToInt32(numb, 0);
        }

        public Array Reverse(Array a) {     //НЕ МОГЛИ СРАЗУ ТАК СДЕЛАТЬ ШТОЛЕ
            Array.Reverse(a);
            return a;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e) {
            button2.Enabled = (listBox1.SelectedIndices.Count == 0) ? false : true;
        }
    }

    public class Counters {                 //ДА, ДА, ЗНАЮ, НО КАК СДЕЛАТЬ ПО-ДРУГОМУ?!
        private static ushort easy = 0;
        private static ushort medium = 0;
        private static ushort pro = 0;
        private static ushort files = 0;

        public static ushort getTotal() {
            return Convert.ToUInt16(easy + medium + pro);
        }

        public static ushort getById(byte value) {
            switch (value) {
                case 0:
                    return easy;
                case 1:
                    return medium;
                case 2:
                    return pro;
                default:
                    return 0;
            }
        }

        public static ushort getEasy() {
            return easy;
        }

        public static ushort getMedium() {
            return medium;
        }

        public static ushort getPro() {
            return pro;
        }

        public static ushort getFiles() {
            return files;
        }

        public static void setCounters(Form1 form, int e, int m, int p, int f) {
            easy = Convert.ToUInt16(easy + e);
            medium = Convert.ToUInt16(medium + m);
            pro = Convert.ToUInt16(pro + p);
            files = Convert.ToUInt16(files + f);
            form.cEasy.Text = Convert.ToString(easy);
            form.cMedium.Text = Convert.ToString(medium);
            form.cPro.Text = Convert.ToString(pro);
            form.cTracks.Text = Convert.ToString(easy + medium + pro);
            form.cFiles.Text = Convert.ToString(files);
        }
    }
}
