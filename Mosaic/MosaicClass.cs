﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using log4net;

namespace Mosaic
{
    public class MosaicClass
    {
        private static ILog log = LogManager.GetLogger(typeof(MosaicClass));
        private Color[,] avgsMaster;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static int GetDifference(Color source, Color target)
        {
            int dR = Math.Abs(source.R - target.R);
            int dG = Math.Abs(source.G - target.G);
            int dB = Math.Abs(source.B - target.B);
            int diff = Math.Max(dR, dG);
            diff = Math.Max(diff, dB);
            return diff;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bSource"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Color GetTileAverage(Bitmap bSource, int x, int y, int width, int height)
        {
            long aR = 0;
            long aG = 0;
            long aB = 0;
            for (int w = x; w < x + width; w++)
            {
                for (int h = y; h < y + height; h++)
                {
                    Color cP = bSource.GetPixel(w, h);
                    aR += cP.R;
                    aG += cP.G;
                    aB += cP.B;
                }
            }
            aR = aR / (width * height);
            aG = aG / (width * height);
            aB = aB / (width * height);
            return Color.FromArgb(255, Convert.ToInt32(aR), Convert.ToInt32(aG), Convert.ToInt32(aB));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bSource"></param>
        /// <param name="targetColor"></param>
        /// <returns></returns>
        public static Bitmap AdjustHue(Bitmap bSource, Color targetColor)
        {
            Bitmap result = new Bitmap(bSource.Width, bSource.Height);
            for (int w = 0; w < bSource.Width; w++)
            {
                for (int h = 0; h < bSource.Height; h++)
                {
                    // Get current output color
                    Color clSource = bSource.GetPixel(w, h);
                    int R = Math.Min(255, Math.Max(0, ((clSource.R + targetColor.R) / 2)));
                    int G = Math.Min(255, Math.Max(0, ((clSource.G + targetColor.G) / 2)));
                    int B = Math.Min(255, Math.Max(0, ((clSource.B + targetColor.B) / 2)));
                    Color clAvg = Color.FromArgb(R, G, B);

                    result.SetPixel(w, h, clAvg);
                    Application.DoEvents();
                }
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bSource"></param>
        /// <param name="newSize"></param>
        /// <returns></returns>
        private static Bitmap ResizeBitmap(Bitmap bSource, Size newSize)
        {
            Bitmap result = new Bitmap(newSize.Width, newSize.Height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(bSource, 0, 0, newSize.Width, newSize.Height);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void CalculateColorsWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var arguments = e.Argument as object[];
            var worker = sender as BackgroundWorker;
            var image = arguments[0] as Image;
            var height = (decimal)arguments[1];
            var width = (decimal)arguments[2];
            var szTile = new Size((int)width, (int)height);
            Boolean bLoaded = false;
            Bitmap bMaster = null;
            LockBitmap bOut = null;

            /// Notification
            // lblUpdate.Text = 
            worker.ReportProgress(1, String.Format(strings.LoadingMasterFile));

            /// File Load Phase  
            while (!bLoaded)
            {
                try
                {
                    bMaster = new Bitmap((Image)image.Clone());
                    bLoaded = true;
                }
                catch (OutOfMemoryException)
                {
                    GC.WaitForPendingFinalizers();
                }
            }

            /// Notification
            worker.ReportProgress(1, String.Format(strings.AveragingMasterBitmap));

            /// Average Master Image Phase
            int tX = bMaster.Width / szTile.Width;
            int tY = bMaster.Height / szTile.Height;
            this.avgsMaster = new Color[tX, tY];

            /// Notification
            var maximum = tX * tY;
            var progres = 4;
            lock (image)
            {
                for (int x = 0; x < tX; x++)
                {
                    for (int y = 0; y < tY; y++)
                    {
                        avgsMaster[x, y] = GetTileAverage(bMaster, x * szTile.Width, y * szTile.Height, szTile.Width, szTile.Height);
                        Rectangle r = new Rectangle(szTile.Width * x, szTile.Height * y, szTile.Width, szTile.Height);
                        worker.ReportProgress((int)((double)x / tX * 100), String.Format(strings.AveragingMasterBitmap));

                        using (Graphics g = Graphics.FromImage(image))
                        {
                            g.FillRectangle(new SolidBrush(avgsMaster[x, y]), r);
                        }
                    }
                }
            }

            /// Output Load Phase                
            bLoaded = false;
            while (!bLoaded)
            {
                try
                {
                    bOut = new LockBitmap(bMaster);
                    bLoaded = true;
                }
                catch (OutOfMemoryException)
                {
                    GC.WaitForPendingFinalizers();
                }
            }

            /// Close Master Image Phase
            bMaster.Dispose();
            bMaster = null;

            /// Notification
            //lblUpdate.Text = String.Format("Loading and Resizing Tile Images...");
            e.Result = image;
        }

        internal void CalculateMosaic(object sender, DoWorkEventArgs e)
        {
            object[] arguments = e.Argument as object[];
            var image = arguments[0] as Image;
            List<string> tilesNames = arguments[1] as List<string>;
            var height = (int)arguments[2];
            var width = (int)arguments[3];
            
            var worker = sender as BackgroundWorker;

            worker.ReportProgress(0, String.Format(strings.LoadingAndResizingTiles));
            double maximum = tilesNames.Count;

            /// Tile Load And Resize Phase
            String errorFiles = String.Empty;
            Bitmap bTile;
            var sizeTile = new Size(width, height);
            int tX = image.Width / sizeTile.Width;
            int tY = image.Height / sizeTile.Height;
            
            Dictionary<string, Color> tilesColors = new Dictionary<string, Color>();
            LockBitmap bOut = null;
            /// Notification
            //pgbUpdate.Maximum = fTiles.Count();
            //pgbUpdate.Value = 0;

            if (Directory.Exists("tiles\\"))
            {
                Directory.Delete("tiles\\",true);
            }
            Directory.CreateDirectory("tiles\\");

            int index = 0;
            foreach (string tilePath in tilesNames)
            {
                try
                {
                    index++;
                    var tilename = "tiles\\" + index.ToString() + ".bmp";
                    using (Stream stream = new FileStream(tilePath, FileMode.Open))
                    {
                        using (bTile = (Bitmap)Bitmap.FromStream(stream))
                        {
                            bTile = ResizeBitmap(bTile, sizeTile);
                            bTile.Save(tilename);
                            tilesColors.Add(tilename, GetTileAverage(bTile, 0, 0, sizeTile.Width, sizeTile.Height));
                            worker.ReportProgress((int)((index / maximum) * 100), String.Format(strings.LoadingAndResizingTiles));
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    log.ErrorFormat("{0}: {1}", tilePath, ex.Message);
                    errorFiles += String.Format("{0}: {1}\r\n", tilePath, ex.Message);
                }
                catch (OutOfMemoryException ex)
                {
                    log.ErrorFormat("Problem with image {0}", tilePath);
                    log.Error(ex.Message, ex);
                    GC.WaitForPendingFinalizers();
                }
            }


            if (errorFiles.Length > 0)
            {
                throw new Exception(errorFiles);
            }

            // Notification
            //lblUpdate.Text = String.Format("Averaging Tiles...");
            //pgbUpdate.Maximum = bTiles.Count();
            //pgbUpdate.Value = 0;

            // Iterative Replacement Phase / Search Phase
            if (tilesColors.Count > 0)
            {
                /// Notification
                //lblUpdate.Text = String.Format("Applying Search Pattern...");
                //pgbUpdate.Maximum = tX * tY;
                //pgbUpdate.Value = 0;


                Random r = new Random();

                //TODO: get as parameter
                //if (bAdjustHue)
                if (false)
                {
                    // Adjust hue - get the first (random) tile found and adjust its colours
                    // to suit the average
                    List<Tile> tileQueue = new List<Tile>();
                    Tile tFound = null;
                    int maxQueueLength = Math.Min(1000, Math.Max(0, tilesColors.Count - 50));

                    for (int x = 0; x < tX; x++)
                    {
                        for (int y = 0; y < tY; y++)
                        {
                            int i = 0;
                            // Check if it's the same as the last (X)?
                            if (tileQueue.Count > 1)
                            {
                                //while (tileQueue.Contains(tilesColors[i]))
                                //{
                                //    i = r.Next(tilesColors.Count);
                                //}
                            }

                            // Add to the 'queue'
                            tFound = null;//tilesColors[i];
                            if ((tileQueue.Count >= maxQueueLength) && (tileQueue.Count > 0))
                            {
                                tileQueue.RemoveAt(0);
                            }
                            tileQueue.Add(tFound);

                            // Adjust the hue
                            //Bitmap bAdjusted = AdjustHue(tFound.getBitmap(), avgsMaster[x, y]);

                            // Apply found tile to section
                            for (int w = 0; w < sizeTile.Width; w++)
                            {
                                for (int h = 0; h < sizeTile.Height; h++)
                                {
                                    // bOut.SetPixel(x * szTile.Width + w, y * szTile.Height + h, bAdjusted.GetPixel(w, h));
                                }
                            }
                            // Increment the progress bar
                            // pgbUpdate.Value++;
                        }
                    }
                }
                else
                {
                    // Don't adjust hue - keep searching for a tile close enough
                    for (int x = 0; x < tX; x++)
                    {
                        for (int y = 0; y < tY; y++)
                        {
                            // Reset searching threshold
                            int threshold = 0;
                            int i = 0;
                            int searchCounter = 0;
                            Bitmap tFound = null;
                            while (tFound == null)
                            {
                                i = r.Next(tilesColors.Count);
                                string name = "tiles\\" + i.ToString() + ".bmp";
                                if (GetDifference(this.avgsMaster[x, y], tilesColors["tiles\\" + i.ToString() + ".bmp"]) < threshold)
                                {
                                    //TODO: tFound = tilesColors[name];
                                    Application.DoEvents();
                                }
                                else
                                {
                                    searchCounter++;
                                    if (searchCounter >= tilesColors.Count)
                                    {
                                        threshold += 5;
                                    }
                                    Application.DoEvents();
                                }
                            }
                            // Apply found tile to section
                            for (int w = 0; w < sizeTile.Width; w++)
                            {
                                for (int h = 0; h < sizeTile.Height; h++)
                                {
                                    bOut.SetPixel(x * sizeTile.Width + w, y * sizeTile.Height + h, tFound.GetPixel(w, h));
                                    Application.DoEvents();
                                }
                            }
                            // Increment the progress bar
                            //pgbUpdate.Value++;
                        }
                    }
                }
            }

            // Close Files Phase

            /// Notification
            //lblUpdate.Text = String.Format("Job Completed");
        }
    }

    public class Tile
    {
        private Bitmap bitmap;
        private Color color;

        public Bitmap getBitmap()
        {
            return bitmap;
        }
        public Color getColor()
        {
            return color;
        }
        public void setColor(Color average)
        {
            color = average;
        }

        public Tile(Bitmap bSource, Color cSource)
        {
            bitmap = bSource;
            color = cSource;
        }

        public void Close()
        {
            bitmap.Dispose();
            bitmap = null;
        }
    }
}
