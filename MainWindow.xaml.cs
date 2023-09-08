using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AtlasCutter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Bitmap> loadedImages = new List<Bitmap>();
        private System.Windows.Point start;
        private System.Windows.Point end;
        private bool isDragging = false;
        private double imageScale = 0.0;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // ShowImage(loadedImages[0]);
        }

        private void OnLoadImagesClicked(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };

            if (dlg.ShowDialog() == true)
            {
                loadedImages.Clear();

                foreach (var filePath in dlg.FileNames)
                {
                    Bitmap image = new Bitmap(filePath);
                    image.Tag = filePath;
                    loadedImages.Add(image);
                }

                if (loadedImages.Count > 0)
                {
                    ShowImage(loadedImages[0]);
                }
                imageDisplay.UpdateLayout();
            }
        }

        public Bitmap ResizeBitmap(Bitmap sourceBMP, double width, double height)
        {
            imageScale = Math.Min(width / sourceBMP.Width, height / sourceBMP.Height);

            int scaleWidth = (int)(sourceBMP.Width * imageScale);
            int scaleHeight = (int)(sourceBMP.Height * imageScale);

            /*            Debug.WriteLine(imageScale);
                        Debug.WriteLine(width);
                        Debug.WriteLine(height);
                        Debug.WriteLine(sourceBMP.Width);
                        Debug.WriteLine(sourceBMP.Height);
                        Debug.WriteLine(imageDisplay.ActualWidth);
                        Debug.WriteLine(imageDisplay.ActualHeight);
                        Debug.WriteLine(imageCanvas.ActualWidth);
                        Debug.WriteLine(imageCanvas.ActualHeight);*/

            Bitmap result = new Bitmap(scaleWidth, scaleHeight);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(sourceBMP, 0, 0, scaleWidth, scaleHeight);
            }

            return result;
        }


        private void ShowImage(Bitmap originalBitmap)
        {
            Bitmap displayedBitmap;

            if (originalBitmap.Width > imageCanvas.ActualWidth || originalBitmap.Height > imageCanvas.ActualHeight)
            {
                displayedBitmap = ResizeBitmap(originalBitmap, imageCanvas.ActualWidth, imageCanvas.ActualHeight);
                //originalBitmap.Dispose(); // Dispose the original if you're not using it elsewhere
            }
            else
            {
                displayedBitmap = originalBitmap; // If the image is smaller, use it as-is
            }

            MemoryStream memory = new MemoryStream();
            displayedBitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png); // Save the bitmap to a memory stream
            memory.Position = 0;

            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.EndInit();

            imageDisplay.Source = bitmapImage;
        }

        private List<int> GetChoppingCoordinates()
        {
            int originalX = (int)(Canvas.GetLeft(selectionRectangle) / imageScale);
            int originalY = (int)(Canvas.GetTop(selectionRectangle) / imageScale);
            int originalWidth = (int)(selectionRectangle.Width / imageScale);
            int originalHeight = (int)(selectionRectangle.Height / imageScale);

            return new List<int>() { originalX, originalY, originalWidth, originalHeight };
        }

        public Bitmap CropImage(Bitmap source, System.Drawing.Rectangle section)
        {
            // Create a new blank bitmap with the size of the rectangle
            Bitmap bmp = new Bitmap(section.Width, section.Height);

            bmp.SetResolution(source.HorizontalResolution, source.VerticalResolution); // Copy the DPI settings

            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Draw the specified section of the source bitmap onto the new bitmap
                g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);
            }

            return bmp;
        }

        private void OnSaveImagesClicked(object sender, RoutedEventArgs e)
        {
            if (loadedImages.Count == 0 || selectionRectangle.Visibility != Visibility.Visible)
                return;

            var choppingCoord = GetChoppingCoordinates();

            var destRect = new System.Drawing.Rectangle(
                choppingCoord[0],
                choppingCoord[1],
                choppingCoord[2],
                choppingCoord[3]);

            Debug.WriteLine($"{choppingCoord[0]}, {choppingCoord[1]}, {choppingCoord[2]}, {choppingCoord[3]}");

            // Use FolderBrowserDialog to select save directory
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                string saveDirectory = folderDialog.SelectedPath;

                int counter = 1;
                foreach (var image in loadedImages)
                {
                    /*Debug.WriteLine(image);
                    var temp = image;*/
                    Bitmap cropped = CropImage(image, destRect);

                    // Suggest a filename
                    string originalFilename = System.IO.Path.GetFileNameWithoutExtension(image.Tag.ToString()); // Stored original file path in image.Tag
                    string fileExtension = System.IO.Path.GetExtension(image.Tag.ToString());
                    string newFilename = $"{originalFilename}_cropped{fileExtension}";
                    string fullPath = System.IO.Path.Combine(saveDirectory, newFilename);

                    // Check if file already exists, if so, append a counter
                    while (File.Exists(fullPath))
                    {
                        newFilename = $"{originalFilename}_cropped_{counter}{fileExtension}";
                        fullPath = System.IO.Path.Combine(saveDirectory, newFilename);
                        counter++;
                    }

                    // Image format
                    System.Drawing.Imaging.ImageFormat format = System.Drawing.Imaging.ImageFormat.Png; // Default to PNG
                    switch (fileExtension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            format = System.Drawing.Imaging.ImageFormat.Jpeg;
                            break;
                        case ".bmp":
                            format = System.Drawing.Imaging.ImageFormat.Bmp;
                            break;
                        case ".gif":
                            format = System.Drawing.Imaging.ImageFormat.Gif;
                            break;
                        case ".png":
                            format = System.Drawing.Imaging.ImageFormat.Png;
                            break;
                        case ".tif":
                        case ".tiff":
                            format = System.Drawing.Imaging.ImageFormat.Tiff;
                            break;
                        case ".ico":
                            format = System.Drawing.Imaging.ImageFormat.Icon;
                            break;
                        // Add more formats as needed
                        default:
                            // Handle unknown formats or throw an exception
                            break;
                    }

                    // Save the image
                    cropped.Save(fullPath, format);
                    cropped.Dispose();
                }
            }
        }


        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (loadedImages.Count == 0) return;

            isDragging = true;
            start = e.GetPosition(imageCanvas);

            selectionRectangle.Visibility = Visibility.Visible;
            Canvas.SetLeft(selectionRectangle, start.X);
            Canvas.SetTop(selectionRectangle, start.Y);
            selectionRectangle.Width = 0;
            selectionRectangle.Height = 0;
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isDragging)
            {
                end = e.GetPosition(imageCanvas);

                var x = Math.Min(end.X, start.X);
                var y = Math.Min(end.Y, start.Y);
                var width = Math.Abs(end.X - start.X);
                var height = Math.Abs(end.Y - start.Y);

                Canvas.SetLeft(selectionRectangle, x);
                Canvas.SetTop(selectionRectangle, y);
                selectionRectangle.Width = width;
                selectionRectangle.Height = height;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }
    }
}
