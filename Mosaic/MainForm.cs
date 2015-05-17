﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Mosaic
{
    public partial class MainForm : Form
    {
        private MosaicClass mosaicClass;

        public MainForm()
        {
            InitializeComponent();
            this.gbxMaster.Text = strings.MasterImage;
            this.btnBrowse.Text = strings.Browse;
            this.btnAdd.Text = strings.Add;
            this.btnRemove.Text = strings.Remove;
            this.btnGo.Text = strings.Go;
            this.lblAddFirst.Text = strings.AddTilesFirst;
            this.cbxAdjustTiles.Text = strings.AdjustHue;
            this.lblHeight.Text = strings.Height;
            this.lblWidth.Text = strings.Width;
            this.gbxTiles.Text = strings.Tiles;
            this.gbxMosaic.Text = strings.Mosaic;
            
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog oD = new OpenFileDialog();
            oD.Multiselect = false;
            this.mosaicClass = new MosaicClass();
            var backgroundCalculateColorsOnPicture = new BackgroundWorker();
            backgroundCalculateColorsOnPicture.WorkerReportsProgress = true;
            backgroundCalculateColorsOnPicture.DoWork += this.mosaicClass.CalculateColorsWork;
            backgroundCalculateColorsOnPicture.ProgressChanged += this.CalculateColorsProgressChanged;
            backgroundCalculateColorsOnPicture.RunWorkerCompleted += this.CalculateColorsCompleted;

            if (oD.ShowDialog() == DialogResult.OK)
            {
                tbxBrowse.Text = oD.FileName;
                this.pictureBox.Image = Image.FromFile(oD.FileName);
            }

            backgroundCalculateColorsOnPicture.RunWorkerAsync(new object[] { Image.FromFile(oD.FileName), this.nudHeight.Value, nudWidth.Value });
        }

        private void CalculateColorsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //Set all to 0;
            pgbOperation.Value = 0;
            lblOperation.Text = strings.ColorsCalculated;
            var image = e.Result as Image;
            this.pictureBox.Image = image;
            this.pictureBox.Refresh();
            var calculateMosaicBackgroundWorker = new BackgroundWorker();
            calculateMosaicBackgroundWorker.ProgressChanged += CalculateColorsProgressChanged;
            calculateMosaicBackgroundWorker.RunWorkerCompleted += calculateMosaicBackgroundWorker_RunWorkerCompleted;
            calculateMosaicBackgroundWorker.DoWork += this.mosaicClass.CalculateMosaic;
            // calculateMosaicBackgroundWorker.RunWorkerAsync(new object[] { e.Result });

        }

        void calculateMosaicBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void CalculateColorsProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var progres = e.ProgressPercentage;
            var v = e.UserState as String;
            this.pgbOperation.Value = progres;
            this.lblOperation.Text = v;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog oD = new FolderBrowserDialog();
            if (oD.ShowDialog() == DialogResult.OK)
            {
                if (Directory.Exists(oD.SelectedPath))
                {
                    DirectoryInfo di = new DirectoryInfo(oD.SelectedPath);
                    foreach (FileInfo fN in di.GetFiles())
                    {
                        if (!(lbxTiles.Items.Contains(fN.FullName)))
                        {
                            lbxTiles.Items.Add(fN.FullName);
                        }
                        Application.DoEvents();
                    }
                }
                else
                {
                    MessageBox.Show("Directory doesn't exist");
                }
            }
            if (this.lbxTiles.Items.Count > 15)
            {
                btnGo.Enabled = true;
                lblAddFirst.Visible = false;
            }
            else
            {
                btnGo.Enabled = false;
                lblAddFirst.Visible = true;
                lblAddFirst.Text = "You have to add at least 15 tiles";
            }
        }



        private void btnRemove_Click(object sender, EventArgs e)
        {
            List<String> fNS = new List<String>();
            for (int i = 0; i < lbxTiles.Items.Count; i++)
            {
                if (!(lbxTiles.SelectedIndices.Contains(i)))
                {
                    fNS.Add((String)lbxTiles.Items[i]);
                }
            }
            lbxTiles.Items.Clear();
            lbxTiles.Items.AddRange(fNS.ToArray());
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                Size szTile = new Size(Convert.ToInt16(nudWidth.Value), Convert.ToInt16(nudHeight.Value));
                //LockBitmap test = MosaicClass.GenerateMosaic(tbxBrowse.Text, lbxTiles.Items.Cast<String>().ToArray(), szTile, lblOperation, pgbOperation, cbxAdjustTiles.Checked, tbxCache.Text, this.pictureBox);
                //test.Save("test.bmp");
                //pictureBox.Image = test.source;
            }
            catch (Exception x)
            {
                MessageBox.Show(this, x.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void btnCache_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog oD = new FolderBrowserDialog();
            if (oD.ShowDialog() == DialogResult.OK)
            {
                //tbxCache.Text = oD.SelectedPath;
            }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            btnGo.Enabled = false;
            lblAddFirst.Visible = true;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var about = new AboutBox();
            about.ShowDialog();
        }
    }
}
