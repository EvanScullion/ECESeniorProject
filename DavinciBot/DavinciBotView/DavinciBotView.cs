﻿using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DavinciBotView
{
    public partial class DavinciBotView : Form
    {
        public string loadedImagePath;
        public string loadedImageName;
        public string gCodeFilePath;
        private Image contourCopy;
        private bool pictureTakenWithCamera = false;
        private bool imageLoaded = false;
        private bool invertedContour = false;
        private static bool startedCamera = false;
        private static FilterInfoCollection Devices;
        private static VideoCaptureDevice frame;
        private const string MASTER_GCODE_FILE = "commands.gco";
        private const int DEFAULT_THRESHOLD_VALUE = 100;
        private const string MASTER_DIRECTORY = "../../../../Image_Processor_Files";
        private const int AUTO_SCALE_MAX_HEIGHT = 300;
        private int AUTO_SCALE_MAX_WIDTH = 300;
        public const string FINAL_SCALED_IMAGE = "resizedImage.bmp";
        public const string FIRST_SCALED_IMAGE = "firstScaledImage.bmp";
        private LinkedList<RecentPictureObject> recentPictures = new LinkedList<RecentPictureObject>();
        private List<PictureBox> recentPictureBoxes = new List<PictureBox>(6);
        private int CAMERA_DEVICE_NUMBER = 5;
        private bool printingPaused = false;
        private DaVinciBotClient client = new DaVinciBotClient();

        //Customize form objects in here
        public DavinciBotView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize all main components of the GUI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DavinciBotView_Load(object sender, EventArgs e)
        {
            //videoSource = new VideoCaptureDevice();
            Devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            frame = new VideoCaptureDevice(Devices[CAMERA_DEVICE_NUMBER].MonikerString);
            EnableImageControls(false);
            EnableCameraControls(false);
            EnableGcodeControls(false);
            EnablePrintingControls(false);

            this.ActiveControl = uploadImageFromFileButton;

            recentPictureBoxes.Add(recentPicture0);
            recentPictureBoxes.Add(recentPicture1);
            recentPictureBoxes.Add(recentPicture2);
            recentPictureBoxes.Add(recentPicture3);
            recentPictureBoxes.Add(recentPicture4);
            recentPictureBoxes.Add(recentPicture5);
        }

        /// <summary>
        /// Enables or disables buttons that can only be used when an image is loaded
        /// </summary>
        /// <param name="m"></param>
        private void EnableImageControls(bool m)
        {
            trackBar1.Enabled = m;
            thresholdNumberBox.Enabled = m;
            invertCheckBox.Enabled = m;
            generateGcodeButton.Enabled = m;
        }
        private void EnableCameraControls(bool m)
        {
            startCameraButton.Enabled = !m;
            takePictureButton.Enabled = m;
            stopCameraButton.Enabled = m;
            saveCameraImageButton.Enabled = m;
            clearImageButton.Enabled = m;
        }
        private void EnableGcodeControls(bool m)
        {
            generateGcodeButton.Enabled = m;
            printingPaused = false;
        }
        private void EnablePrintingControls(bool m)
        {
            startPrintingButton.Enabled = m;
            stopPrintingButton.Enabled = m;
            pausePrintingButton.Enabled = m;
        }

        private void LoadFromFileToolbarButton_Click(object sender, EventArgs e)
        {
            HandleUploadedImage(sender, e);
        }

        private void ImageToBeDrawnBox_Click(object sender, EventArgs e)
        {
            HandleUploadedImage(sender, e);
        }

        /// <summary>
        /// Loads gcode from file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadGCodeFromFileButton_Click(object sender, EventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;
            OpenFileDialog openFile = new OpenFileDialog();

            //TODO: Enter the most recently accessed directory instead of c:\\ 
            // openFile.InitialDirectory = "c:\\";
            openFile.InitialDirectory = "C:\\Documents and Settings\\USER\\Recent";
            openFile.Filter = "G-Code Files (*.gco)|*.gco";
            openFile.FilterIndex = 2;
            openFile.RestoreDirectory = true;

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                //Get the path of specified file
                filePath = openFile.FileName;
                gCodeFilePath = filePath;
                CopyFile(gCodeFilePath, MASTER_GCODE_FILE);
                EnableGcodeControls(true);
                generateGcodeButton.Enabled = false;
                loadedGcodeTextBox.Text = gCodeFilePath;
            }
            // MessageBox.Show(fileContent, "File Content at path: " + filePath, MessageBoxButtons.OK);
        }

        /// <summary>
        ///Asks the user if they really want to close the program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DavinciBotView_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dialogue = MessageBox.Show("Are you sure you want to exit?", "Exit",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (dialogue != DialogResult.Yes)
                e.Cancel = true;
        }

        /// <summary>
        /// Processes preview image to rastor-style gcode file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GenerateGcodeButton_Click(object sender, EventArgs e)
        {
            DialogResult dialogue = MessageBox.Show(
                    "Would you like to save a copy of your G-Code file?", "DaVinciBot",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (dialogue == DialogResult.Cancel)
            {
                return;
            }
            else
            {
                string oldDir = Environment.CurrentDirectory;
                RunPythonScript("gcode", 0);
                Environment.CurrentDirectory = oldDir;

                if (dialogue == DialogResult.Yes)
                {
                    SaveFileDialog saveConvertedImage = new SaveFileDialog();
                    saveConvertedImage.Filter = "G-Code Files (*.gco)|*.gco";
                    if (saveConvertedImage.ShowDialog() == DialogResult.OK)
                    {
                        CopyFile(MASTER_GCODE_FILE, saveConvertedImage.FileName);
                    }
                }
            }

            startPrintingButton.Enabled = true;
        }

        /// <summary>
        /// Mode is either "gcode" or "contour"
        /// </summary>
        /// <param name="mode"></param>
        private void RunPythonScript(string mode, int threshold)
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                Environment.CurrentDirectory = MASTER_DIRECTORY;
                string pScript = BuildPythonScript(mode);

                runspace.Open();
                using (Pipeline pipeline = runspace.CreatePipeline())
                {
                    pipeline.Commands.AddScript(pScript);
                    pipeline.Commands.Add("Out-String");
                    Collection<PSObject> results = pipeline.Invoke();
                }
                //Put a message box here
                runspace.Dispose();
            }
            return;
        }

        /// <summary>
        /// Builds the python scripts for either the gcode generator or the findContour method
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>

        private string BuildPythonScript(string mode)
        {
            string script;
            switch (mode)
            {
                case "gcode":
                    {
                        //need to change this to scale image properly based on user inputs
                        double x_offset_mm = 0;
                        double y_offset_mm = 0;
                        double output_image_horizontal_size_mm = 400;
                        double pixel_size_mm = .5;
                        double feedrate = 100;
                        double max_laser_power = 255;
                        int number_of_colors = 2;

                        script = "python "
                            + "./imgcode.py "
                            + '"'
                            + FINAL_SCALED_IMAGE
                            + '"'
                            + " ./commands.gco "
                            + x_offset_mm + ' '
                            + y_offset_mm + ' '
                            + output_image_horizontal_size_mm + ' '
                            + pixel_size_mm + ' '
                            + feedrate + ' '
                            + max_laser_power + ' '
                            + number_of_colors;

                        break;
                    }
                case "contour":
                    {
                        string contourFile;
                        if (invertedContour)
                        {
                            contourFile = "./contours0.py";
                        }
                        else
                        {
                            contourFile = "./contours.py";
                        }
                        script = "python "
                                + contourFile
                                + " --image_file "
                                + '"'
                                + FINAL_SCALED_IMAGE
                                + '"'
                                + " --threshold "
                                + trackBar1.Value;
                        break;
                    }
                default:
                    script = "";
                    break;
            }
            return script;
        }

        /// <summary>
        /// Calls python script to find contours in an image and update the image preview
        /// Referenced from: https://blogs.msdn.microsoft.com/kebab/2014/04/28/executing-powershell-scripts-from-c/
        /// </summary>
        private void FindContour(int threshold)
        {
            string oldDir = Environment.CurrentDirectory;
            RunPythonScript("contour", threshold);

            using (var fs = new FileStream("preview_contour.jpg", System.IO.FileMode.Open))
            {
                var bmp = new Bitmap(fs);
                var map = (Bitmap)bmp.Clone();
                previewImageBox.Image = map; //(Bitmap)bmp.Clone();
                contourCopy = map;
            }
            Environment.CurrentDirectory = oldDir;
        }

        /// <summary>
        /// Updates image preview window when threshold for image processing has changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImagePreviewThresholdChanged(object sender, EventArgs e)
        {
            HandleThresholdValueChange("trackbar");
            thresholdNumberBox.Value = trackBar1.Value;
        }

        /// <summary>
        /// Adjusts image processing threshold
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrackBar1_MouseUp(object sender, MouseEventArgs e)
        {
            HandleThresholdValueChange("trackbar");

        }

        /// <summary>
        /// Toggles between proessing with contour.py and contour 2.py
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InvertCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void StartCameraButton_Click(object sender, EventArgs e)
        {
            uploadImageFromFileTextbox.Text = "";
            StartCamera();
        }

        /// <summary>
        /// Turn on camera to take pictures
        /// References: https://www.youtube.com/watch?v=A4Qcq9GOvGQ
        /// </summary>
        private void StartCamera()
        {
            ResetLoadedImage();

            if (imageLoaded)
            {

            }
            //STOP ALL PREVIOUS CAMERAS OR YOU GET A THREADING ISSUE
            frame.NewFrame += new AForge.Video.NewFrameEventHandler(FrameEvent);
            frame.Start();
            startedCamera = true;
            EnableCameraControls(false); //Reset
            takePictureButton.Enabled = true;
            stopCameraButton.Enabled = true;
            startCameraButton.Enabled = false;
        }

        private void StopCamera()
        {
            frame.SignalToStop();
            frame.NewFrame -= new NewFrameEventHandler(FrameEvent);
            frame.WaitForStop();
            while (frame.IsRunning)
            {
                frame.Stop();
            }

            //Ask if user wants to save camera image

            EnableCameraControls(false);


        }

        private void FrameEvent(object sender, NewFrameEventArgs e)
        {
            try
            {
                Image temp = (Image)e.Frame.Clone();
                temp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                OurPictureBox.Image = temp;

            }
            catch (Exception) //need e?
            {
                //throw;
            }
        }

        private void TakePictureButton_Click(object sender, EventArgs e)
        {
            frame.SignalToStop();
            frame.NewFrame -= new NewFrameEventHandler(FrameEvent);
            frame.WaitForStop();
            while (frame.IsRunning)
            {
                frame.Stop();
            }
            saveCameraImageButton.Enabled = true;
            clearImageButton.Enabled = true;
            takePictureButton.Enabled = false;
            startCameraButton.Enabled = true;
            Image temp = (Image)OurPictureBox.Image.Clone();
            //Puts it in the main box. Save this for later.
            OurPictureBox.Image = temp;
            pictureTakenWithCamera = true;
            //make sure to update loadedImagePath to this temp file
            //uploadImageFromFileTextbox 
            //SaveFileDialog saveCameraImage = new SaveFileDialog
            string oldDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = MASTER_DIRECTORY;

            loadedImagePath = "temp.jpg";
            OurPictureBox.Image.Save(loadedImagePath);
            //AUTOSCALE IMAGE HERE
            var bmp = AutoScaleImage("temp.jpg");
            bmp.Save(FINAL_SCALED_IMAGE);

            AddToRecentPictures();
            Environment.CurrentDirectory = oldDir;

            FindContour(DEFAULT_THRESHOLD_VALUE);
            imageLoaded = true;
            EnableImageControls(true);

        }

        private void StopCameraButton_Click(object sender, EventArgs e)
        {
            if (imageLoaded)
            {
                DialogResult dialogue = MessageBox.Show(
                           "Would you like to save a copy of your photo?", "DaVinciBot",
                           MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dialogue == DialogResult.Cancel)
                {
                    return;
                }
                else if (dialogue == DialogResult.Yes && imageLoaded)
                {
                    SaveCameraImage();
                }
            }
            StopCamera();
            //OurPictureBox.Image = null;
            ResetLoadedImage();
        }

        /// <summary>
        /// Copies contents of one file to another given two string filenames.
        /// </summary>
        /// <param name="fromFile"></param>
        /// <param name="toFile"></param>
        private void CopyFile(string fromFile, string toFile)
        {
            string line;
            using (System.IO.StreamWriter output = new System.IO.StreamWriter(toFile, true))
            {
                System.IO.StreamReader file = new System.IO.StreamReader(fromFile);
                while ((line = file.ReadLine()) != null)
                {
                    output.WriteLine(line);
                }
                file.Close();
            }
        }

        /// <summary>
        /// When a user enters a threshold value manually and clicks enter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThresholdNumberBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == Convert.ToChar(Keys.Enter))
            {
                e.Handled = true;
                e.KeyChar = (char)46;
                int val = (int)thresholdNumberBox.Value;
                FindContour(val);
                trackBar1.Value = val;
                this.ActiveControl = null;
            }
        }

        private void ThresholdNumberBox_ValueChanged(object sender, EventArgs e)
        {
            HandleThresholdValueChange("box");
        }
        /// <summary>
        /// Threshold trackbar event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrackBar1_ValueChanged(object sender, EventArgs e)
        {

        }
        /// <summary>
        /// Error handling for threshold changes.
        /// mode param can either be "trackbar" or "box", whichever event was triggered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <param name="mode"></param>
        private void HandleThresholdValueChange(string mode)
        {
            if (!imageLoaded)
            {
                EnableImageControls(false);
            }
            else
            {
                switch (mode)
                {
                    case "trackbar":
                        {
                            thresholdNumberBox.Value = trackBar1.Value;
                            break;
                        }
                    case "box":
                        {
                            trackBar1.Value = (int)thresholdNumberBox.Value;
                            break;
                        }
                    default:
                        {
                            trackBar1.Value = DEFAULT_THRESHOLD_VALUE;
                            thresholdNumberBox.Value = DEFAULT_THRESHOLD_VALUE;
                            break;
                        }
                }
                FindContour(trackBar1.Value);
            }
        }

        /// <summary>
        ///Closes camera resources before exiting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DavinciBotView_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (frame != null)
            {
                StopCamera();
            }
            Application.Exit();
        }

        /// <summary>
        /// General error handling for operations that require prerequisites
        /// </summary>
        private void ReportImageOperationError()
        {
            DialogResult dialogue = MessageBox.Show("You must select an image first.", "Error",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
        }

        private void InvertCheckBox_Click(object sender, EventArgs e)
        {
            if (imageLoaded)
            {
                invertedContour = !invertedContour;
                FindContour(trackBar1.Value);
            }
            else
            {
                invertCheckBox.Checked = false;
            }
        }

        private void ThresholdControlPanel_Click(object sender, EventArgs e)
        {
            if (!trackBar1.Enabled && !thresholdNumberBox.Enabled && !invertCheckBox.Enabled)
                ReportImageOperationError();
        }

        private void Trackbar1Panel_Click(object sender, EventArgs e)
        {
            ThresholdControlPanel_Click(sender, e);
        }

        private void TrackBar1_Scroll(object sender, EventArgs e)
        {
            HandleThresholdValueChange("trackbar");
        }

        private void ClearImageButton_Click(object sender, EventArgs e)
        {
            OurPictureBox.Image = null;
            previewImageBox.Image = null;
            StartCamera();
        }

        private void UploadImageFromFileButton_Click(object sender, EventArgs e)
        {
            /*
            if (imageLoaded)
            {
                DialogResult dialogue = MessageBox.Show(
                        "Would you like to save your converted photo?", "DaVinciBot",
                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (dialogue == DialogResult.Cancel)
                {
                    return;
                }
                else if (dialogue == DialogResult.Yes)
                {
                    //SaveContourFile();
                }
            }
            */
            HandleUploadedImage(sender, e);
        }

        private void SaveCameraImage()
        {
            SaveFileDialog saveConvertedImage = new SaveFileDialog();
            saveConvertedImage.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg; *.jpeg; *.png; *.bmp ";
            if (saveConvertedImage.ShowDialog() == DialogResult.OK)
            {
                OurPictureBox.Image.Save(saveConvertedImage.FileName);
            }
        }

        private void HandleUploadedImage(object sender, EventArgs e)
        {
            var fileContent = string.Empty;
            var filePath = string.Empty;
            OpenFileDialog openFile = new OpenFileDialog();

            //TODO: Enter the most recently accessed directory instead of c:\\ 
            // openFile.InitialDirectory = "c:\\";
            openFile.InitialDirectory = "C:\\Documents and Settings\\USER\\Recent";
            openFile.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg; *.jpeg; *.png; *.bmp ";
            openFile.FilterIndex = 2;
            openFile.RestoreDirectory = true;

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                ResetLoadedImage();
                //Get the path of specified file
                filePath = openFile.FileName;

                //PULL OUT INTO FUNCTION
                loadedImagePath = filePath;
                uploadImageFromFileTextbox.Text = loadedImagePath;

                string oldDir = Environment.CurrentDirectory;
                Environment.CurrentDirectory = MASTER_DIRECTORY;

                Bitmap bmp = AutoScaleImage(loadedImagePath);
                bmp.Save(FINAL_SCALED_IMAGE);
                OurPictureBox.Image = (Bitmap)bmp.Clone();
                bmp.Dispose();
                AddToRecentPictures();

                Environment.CurrentDirectory = oldDir;
                HandleThresholdValueChange("");

                FindContour(DEFAULT_THRESHOLD_VALUE);
                //END HERE
                EnableImageControls(true);
                EnableGcodeControls(true);
                imageLoaded = true;
            }
            // MessageBox.Show(fileContent, "File Content at path: " + filePath, MessageBoxButtons.OK);
        }

        private void StartPrintingButton_Click(object sender, EventArgs e)
        {
            client.RunClient();
            startPrintingButton.Enabled = false;
            LoadGCodeFromFileButton.Enabled = false;
            generateGcodeButton.Enabled = false;
        }

        private void SaveCameraImageButton_Click(object sender, EventArgs e)
        {
            SaveCameraImage();
        }

        private void SaveContourFile()
        {
            SaveFileDialog saveConvertedImage = new SaveFileDialog();
            saveConvertedImage.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg; *.jpeg; *.png; *.bmp ";
            if (saveConvertedImage.ShowDialog() == DialogResult.OK)
            {
                //contourCopy.Save(saveConvertedImage.FileName)
                //need an intermediary stream to save this too or it freaks out
                // Image temp = (Image)previewImageBox.Image.Clone();
                //  temp.Save(saveConvertedImage.FileName);

            }
        }
        /// <summary>
        /// 
        /// </summary>
        private Bitmap UserRescaleImage(string filename, double scaleFactor)
        {
            Bitmap resized;

            using (var fs = new System.IO.FileStream(filename, System.IO.FileMode.Open))
            {
                Bitmap original = new Bitmap(fs);

                int scaledWidth = (int)(original.Width * scaleFactor);
                int scaledHeight = (int)(original.Height * scaleFactor);

                resized = new Bitmap(original, new Size(scaledWidth, scaledHeight));
                resized.Save("resizedImage.bmp");
                fs.Dispose();
                original.Dispose();
            }
            //Bitmap original = (Bitmap)Image.FromFile("DSC_0002.jpg");
            return resized;
        }
        private Bitmap AutoScaleImage(string filename)
        {
            //string oldDir = Environment.CurrentDirectory;
            //Environment.CurrentDirectory = MASTER_DIRECTORY;
            Bitmap resized;

            using (var fs = new System.IO.FileStream(filename, System.IO.FileMode.Open))
            {
                Bitmap original = new Bitmap(fs);
                double scaleFactor = 1;
                int oHeight = original.Height;
                int oWidth = original.Width;

                if (oHeight > oWidth && (oHeight > AUTO_SCALE_MAX_HEIGHT))
                {
                    scaleFactor = oHeight / AUTO_SCALE_MAX_HEIGHT;
                }
                else if ((oWidth > oHeight) && (oWidth > AUTO_SCALE_MAX_WIDTH))
                {
                    scaleFactor = oWidth / AUTO_SCALE_MAX_WIDTH;
                }

                int scaledWidth = (int)(original.Width / scaleFactor);
                int scaledHeight = (int)(original.Height / scaleFactor);

                resized = new Bitmap(original, new Size(scaledWidth, scaledHeight));
                resized.Save("firstScaledImage.bmp");
                fs.Dispose();
            }
            //Environment.CurrentDirectory = oldDir;
            //Save it to the recent image array
            return resized;
        }

        private void ResetLoadedImage()
        {
            uploadImageFromFileTextbox.Text = "";
            loadedImagePath = "";
            loadedImageName = "";
            gCodeFilePath = "";
            //private Image contourCopy;
            pictureTakenWithCamera = false;
            imageLoaded = false;
            invertedContour = false;
            invertCheckBox.Checked = false;
            startedCamera = false;
            OurPictureBox.Image = null;
            previewImageBox.Image = null;
            EnableCameraControls(false);
            EnableGcodeControls(false);
            EnableImageControls(false);
            EnablePrintingControls(false);
        }

        /// <summary>
        /// Adds to the recent pictures list
        /// </summary>
        private void AddToRecentPictures()
        {
            using (var fs = new FileStream(FIRST_SCALED_IMAGE, FileMode.Open))
            {
                Bitmap bmp = new Bitmap(fs);
                Bitmap item = (Bitmap)bmp.Clone();
                recentPictures.AddFirst(new RecentPictureObject(bmp, FIRST_SCALED_IMAGE));
            }

            UpdateRecentPictureBoxes();
        }
        /// <summary>
        /// Updates the view with the most recent pictures
        /// </summary>
        private void UpdateRecentPictureBoxes()
        {
            //  IEnumerator<Image> iter = new IEnumerator<Image>();
            int listSize = recentPictures.Count;
            int i = 0;
            foreach (RecentPictureObject o in recentPictures)
            {
                recentPictureBoxes[i].Image = o.Image;
                i++;
                if (i == 6)
                    return;
            }
        }

        private void recentPicture0_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(0);
        }

        private void recentPicture1_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(1);
        }

        private void recentPicture2_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(2);
        }

        private void recentPicture3_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(3);
        }

        private void recentPicture4_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(4);
        }

        private void recentPicture5_Click(object sender, EventArgs e)
        {
            HandleThumbnailClicked(5);
        }

        private void HandleThumbnailClicked(int t)
        {
            RecentPictureObject pic = recentPictures.ElementAt<RecentPictureObject>(t);
            recentPictures.Remove(recentPictures.ElementAt<RecentPictureObject>(t));

            string oldDir = Environment.CurrentDirectory;

            Environment.CurrentDirectory = MASTER_DIRECTORY;

            pic.Image.Save(FINAL_SCALED_IMAGE);
            loadedImagePath = Path.GetFileName(FINAL_SCALED_IMAGE);
            uploadImageFromFileTextbox.Text = loadedImagePath;

            var bmp = pic.Image;
            OurPictureBox.Image = (Bitmap)bmp.Clone();
            AddToRecentPictures(); //fix this to eliminate images showing up twice
            Environment.CurrentDirectory = oldDir;
            HandleThresholdValueChange("");
            FindContour(DEFAULT_THRESHOLD_VALUE);
        }
        /// <summary>
        /// NATE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stopPrintingButton_Click(object sender, EventArgs e)
        {
            client.PauseJob();
        }

        /// <summary>
        /// Toggles between pause/unpause
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pausePrintingButton_Click(object sender, EventArgs e)
        {
            HandlePauseJob();
        }
        private void HandlePauseJob()
        {
            if (printingPaused)
            {
                client.ResumeJob();
                pausePrintingButton.Text = "Pause Printing";
            }
            else
            {
                client.PauseJob();
                pausePrintingButton.Text = "Resume Printing";
            }
            printingPaused = !printingPaused;
        }
    }
}