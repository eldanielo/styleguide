using ServiceHelpers;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media;
using System.Collections.ObjectModel;
using Microsoft.ProjectOxford.Common;
using IntelligentKioskSample.MallKioskPageConfig;
using IntelligentKioskSample.Controls;
using System.Globalization;
using Windows.UI;
using System.Diagnostics;
using Windows.UI.Xaml.Media.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace IntelligentKioskSample.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    [KioskExperience(Title = "Mall Kiosk", ImagePath = "ms-appx:/Assets/mall.png", ExperienceType = ExperienceType.Kiosk)]
    public sealed partial class MallKioskPage : Page
    {
        private MallKioskDemoSettings kioskSettings;
        private Item currentRecommendation;
        private ImageAnalyzer currentTarget;
        public ObservableCollection<EmotionExpressionCapture> EmotionFaces { get; set; } = new ObservableCollection<EmotionExpressionCapture>();
        private string basketurl = "https://www.swarovski.com/is-bin/INTERSHOP.enfinity/WFS/SCO-Web_AT-Site/de_DE/-/EUR/ViewData-Start/1928848753?JumpTarget=ViewRequisition-View&=&=&=";
        public MallKioskPage()
        {
            this.InitializeComponent();

            this.cameraControl.ImageCaptured += CameraControl_ImageCaptured;
            this.cameraControl.CameraRestarted += CameraControl_CameraRestarted;
            this.cameraControl.FilterOutSmallFaces = true;

            this.speechToTextControl.SpeechRecognitionAndSentimentProcessed += OnSpeechRecognitionAndSentimentProcessed;

            this.emotionFacesGrid.DataContext = this;
        }

        private async void CameraControl_CameraRestarted(object sender, EventArgs e)
        {
            await this.ResetRecommendationUI();
        }

        private async Task ResetRecommendationUI()
        {
            this.webView.NavigateToString("");
            this.webView.Visibility = Visibility.Collapsed;

            // We induce a delay here to give the camera some time to start rendering before we hide the last captured photo.
            // This avoids a black flash.
            await Task.Delay(500);

            this.imageFromCameraWithFaces.DataContext = null;
            this.imageFromCameraWithFaces.Visibility = Visibility.Collapsed;
        }

        double oldsentiment = 0.5;
        private async void handleReaction(double sentiment) {

                        
            Item recommendation = null;
            if (this.currentRecommendation != null)
            {
                if (sentiment <= 0.33 && oldsentiment > 0.33)
                {
                    recommendation = getRecommandation(currentTarget);
                    // look for an override for negative sentiment
                    // behaviorAction = this.currentRecommendation.SpeechSentimentBehavior.FirstOrDefault(behavior => string.Compare(behavior.Key, "Negative", true) == 0);
                }
                else if (sentiment >= 0.66 && oldsentiment < 0.65)
                {
                    webView.Navigate(new Uri(basketurl));
                }
            }
            if (recommendation != null)
            {
                webView.Navigate(new Uri(recommendation.Url));
                webView.Visibility = Visibility.Visible;
                this.currentRecommendation = recommendation;
            }
        }
        private void handleReactionforEmotion(double sentiment)
        {
            Item recommendation = null;
            if (this.currentRecommendation != null)
            {
                if (sentiment <= 0.10)
                {
                    recommendation = getRecommandation(currentTarget);
                    // look for an override for negative sentiment
                    // behaviorAction = this.currentRecommendation.SpeechSentimentBehavior.FirstOrDefault(behavior => string.Compare(behavior.Key, "Negative", true) == 0);
                }
                else if (sentiment >= 0.90)
                {
                    webView.Navigate(new Uri(basketurl));
                }

            }
            if (recommendation != null)
            {
                webView.Navigate(new Uri(recommendation.Url));
                webView.Visibility = Visibility.Visible;
                this.currentRecommendation = recommendation;
            }
        }

        private void OnSpeechRecognitionAndSentimentProcessed(object sender, SpeechRecognitionAndSentimentResult e)
        {
            handleReaction(e.TextAnalysisSentiment);
        }
   
        private async void CameraControl_ImageCaptured(object sender, ImageAnalyzer e)
        {

            Debug.WriteLine("image captured");
            this.imageFromCameraWithFaces.DataContext = e;
            this.imageFromCameraWithFaces.Visibility = Visibility.Visible;
            
            // We induce a delay here to give the captured image some time to render before we hide the camera.
            // This avoids a black flash.
            await Task.Delay(50);

            await this.cameraControl.StopStreamAsync();
           await e.AnalyseAsync();
          await  e.DetectFacesAsync(detectFaceAttributes: true, detectFaceLandmarks: true);
            currentTarget = e;

            

           ShowRecommendations(e);

            //e.FaceRecognitionCompleted += (s, args) =>
            //{ 
            //    ShowRecommendations(e);
            //};

        }
        
        static async void MakeRequest(byte[] data)
        {
            var client = new HttpClient();

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "b63705bd5e5d4117943e7175d4f68736");

            var uri = "https://westus.api.cognitive.microsoft.com/vision/v1.0/analyze?" + "visualFeatures=Categories";

            HttpResponseMessage response;

            // Request body;

            using (var content = new ByteArrayContent(data))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response = await client.PostAsync(uri, content);
            }

        }
 

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (!string.IsNullOrEmpty(SettingsHelper.Instance.MallKioskDemoCustomSettings))
            {
                try
                {
                    string escapedContent = SettingsHelper.Instance.MallKioskDemoCustomSettings.Replace("&", "&amp;");
                    this.kioskSettings = await MallKioskDemoSettings.FromContentAsync(escapedContent);
                }
                catch (Exception ex)
                {
                    await Util.GenericApiCallExceptionHandler(ex, "Failure parsing custom recommendation URLs. Will use default values instead.");
                }
            }

            if (this.kioskSettings == null)
            {
                this.kioskSettings = await MallKioskDemoSettings.FromFileAsync("Views\\MallKioskDemoConfig\\MallKioskDemoSettings.xml");
            }

            EnterKioskMode();

            if (string.IsNullOrEmpty(SettingsHelper.Instance.FaceApiKey))
            {
                await new MessageDialog("Missing Face API Key. Please enter a key in the Settings page.", "Missing Face API Key").ShowAsync();
            }

            await this.cameraControl.StartStreamAsync(isForRealTimeProcessing: true);
            this.UpdateWebCamHostGridSize();
            this.StartEmotionProcessingLoop();

            base.OnNavigatedTo(e);
        }

        private void UpdateWebCamHostGridSize()
        {
            this.webCamHostGrid.Width = Math.Round(this.ActualWidth * 0.25);
            this.webCamHostGrid.Height = Math.Round(this.webCamHostGrid.Width / (this.cameraControl.CameraAspectRatio != 0 ? this.cameraControl.CameraAspectRatio : 1.777777777777));
        }

        private void EnterKioskMode()
        {
            ApplicationView view = ApplicationView.GetForCurrentView();
            if (!view.IsFullScreenMode)
            {
                view.TryEnterFullScreenMode();
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            this.isProcessingLoopInProgress = false;
            await this.cameraControl.StopStreamAsync();
            this.speechToTextControl.DisposeSpeechRecognizer();
            base.OnNavigatingFrom(e);
        }

        private Item getRecommandation(ImageAnalyzer image) {
            List<Item> RecommendationList = new List<Item>() {
                new Item("#198CB2", "http://www.swarovski.com/Web_AT/de/949740/product/Galet_Ohrringe.html", null),
                new Item("#B01B32", "http://www.swarovski.com/Web_AT/de/5249350/product/Funk_Ohrringe.html", null),
                 new Item("#07664A", "http://www.swarovski.com/Web_AT/de/5267105/product/Angelic_Ohrringe.html", null),
                  new Item("#B34B16", "http://www.swarovski.com/Web_AT/de/5301077/product/Wood_Crystallized_Drop_Ohrringe,_vergoldet.html", null),
                   new Item("#2456A7", "http://www.swarovski.com/Web_AT/de/5298756/product/Jewel-y_McHue-y_Drop_Ohrringe,_mattes_lila_Finish.html", null),
                    new Item("#05B1A4", "http://www.swarovski.com/Web_AT/de/5298430/product/Moselle_Double-Stud_Ohrringe,_palladiniert.html", null),
                     new Item("#FFFFFF", "http://www.swarovski.com/Web_AT/de/1121080/product/Alana_Ohrstecker.html", null)
                   // new Item("#61616A", "http://www.swarovski.com/Web_AT/de/5271718/product/Crystaldust_Kreolen,_klein,_schwarz.html", null)
            };
       
            if (currentRecommendation != null) {
                RecommendationList = RecommendationList.Where(n => n.Url != currentRecommendation.Url).ToList();
            }
            Color targetColor = GetColorFromHex(image.AnalysisResult.Color.AccentColor);
            List<double> dist = new List<double>();
            double min = 99999999;
            Item recommendation = RecommendationList.FirstOrDefault();
            foreach (Item i in RecommendationList)
            {

                double colordistance = ColourDistance(i.color, targetColor);
                Debug.WriteLine(colordistance);
                if (colordistance < min)
                {
                    min = colordistance;
                    recommendation = i;
                }

            }
            return recommendation;
        }


        //@TODO change recommendation
        private async void ShowRecommendations(ImageAnalyzer image)
        {
            Item recommendation = null;
            //check for face/eye color
            var face = image.DetectedFaces.FirstOrDefault();
            if (face != null)
            {

                Rectangle eyerectangle = new Rectangle();


                eyerectangle.Left = (int)face.FaceLandmarks.PupilLeft.X - 50;
                eyerectangle.Top = (int)face.FaceLandmarks.PupilLeft.Y - 50;

                eyerectangle.Width = 100;//200+ Math.Abs((int)(face.FaceLandmarks.EyeLeftInner.X - face.FaceLandmarks.EyeLeftOuter.X));
                eyerectangle.Height = 100;//200+Math.Abs((int)(face.FaceLandmarks.EyeLeftTop.Y - face.FaceLandmarks.EyeLeftBottom.Y));

                var croppedImage = await Util.GetCroppedBitmapAsync(image.GetImageStreamCallback, eyerectangle) as WriteableBitmap;
                this.imageControl.Source = await Util.GetCroppedBitmapAsync(image.GetImageStreamCallback, eyerectangle) as WriteableBitmap;

                SoftwareBitmap outputBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                    croppedImage.PixelBuffer,
                    BitmapPixelFormat.Bgra8,
                    croppedImage.PixelWidth,
                    croppedImage.PixelHeight
                );

                ImageAnalyzer eyeimg = new ImageAnalyzer(await Util.GetPixelBytesFromSoftwareBitmapAsync(outputBitmap));
                await eyeimg.AnalyseAsync();
                


                this.detectedcolor.Background = new SolidColorBrush( ConvertStringToColor(eyeimg.AnalysisResult.Color.AccentColor));
                recommendation = getRecommandation(eyeimg);
            } else {
                this.detectedcolor.Background = new SolidColorBrush(ConvertStringToColor(image.AnalysisResult.Color.AccentColor));
                recommendation = getRecommandation(image);
            }
            
            if (recommendation != null)
            {
                webView.Navigate(new Uri(recommendation.Url));
                webView.Visibility = Visibility.Visible;
                this.currentRecommendation = recommendation;
            }
        }
        public Color ConvertStringToColor(String hex)
        {

            hex = hex.Replace("#", "");

            byte a = 255;
            byte r = 255;
            byte g = 255;
            byte b = 255;

            int start = 0;

            //handle ARGB strings (8 characters long) 
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                start = 2;
            }

            //convert RGB characters to bytes 
            r = byte.Parse(hex.Substring(start, 2), System.Globalization.NumberStyles.HexNumber);
            g = byte.Parse(hex.Substring(start + 2, 2), System.Globalization.NumberStyles.HexNumber);
            b = byte.Parse(hex.Substring(start + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }


        public static double ColourDistance(Color e1, Color e2)
        {
            long rmean = ((long)e1.R + (long)e2.R) / 2;
            long r = (long)e1.R - (long)e2.R;
            long g = (long)e1.G - (long)e2.G;
            long b = (long)e1.B - (long)e2.B;
            return Math.Sqrt((((512 + rmean) * r * r) >> 8) + 4 * g * g + (((767 - rmean) * b * b) >> 8));
        }

        // weighed distance using hue, saturation and brightness
        // closed match in RGB space
        int closestColor2(List<Color> colors, Color target)
        {
          
            var colorDiffs = colors.Select(n => ColorDiff(n, target)).Min(n => n);
            return colors.FindIndex(n => ColorDiff(n, target) == colorDiffs);
        }

        // color brightness as perceived:
        float getBrightness(Windows.UI.Color c)
        { return (c.R * 0.299f + c.G * 0.587f + c.B * 0.114f) / 256f; }

        // distance between two hues:
        float getHueDistance(float hue1, float hue2)
        {
            float d = Math.Abs(hue1 - hue2); return d > 180 ? 360 - d : d;
        }

        // distance in RGB space
        int ColorDiff(Color c1, Color c2)
        {
            return (int)Math.Sqrt((c1.R - c2.R) * (c1.R - c2.R)
                                   + (c1.G - c2.G) * (c1.G - c2.G)
                                   + (c1.B - c2.B) * (c1.B - c2.B));
        }

        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateWebCamHostGridSize();
        }

        #region Real-time Emotion Feed

        private Task processingLoopTask;
        private bool isProcessingLoopInProgress;
        private bool isProcessingPhoto;
        private bool isEmotionResponseFlyoutOpened;

        private async void OnEmotionTrackingFlyoutOpened(object sender, object e)
        {
            await this.cameraControl.StartStreamAsync();
            this.isEmotionResponseFlyoutOpened = true;
        }

        private async void OnEmotionTrackingFlyoutClosed(object sender, object e)
        {
            this.isEmotionResponseFlyoutOpened = false;
            await this.ResetRecommendationUI();
        }

        private void StartEmotionProcessingLoop()
        {
            this.isProcessingLoopInProgress = true;

            if (this.processingLoopTask == null || this.processingLoopTask.Status != TaskStatus.Running)
            {
                this.processingLoopTask = Task.Run(() => this.ProcessingLoop());
            }
        }


        private IEnumerable<Tuple<Face, IdentifiedPerson>> lastIdentifiedPersonSample;
        private IEnumerable<SimilarFaceMatch> lastSimilarPersistedFaceSample;
        private IdentifiedPerson person;
        private async void findIdendity(ImageAnalyzer e) { 
            await Task.WhenAll(e.IdentifyFacesAsync(), e.FindSimilarPersistedFacesAsync());

            if (!e.IdentifiedPersons.Any())
            {
                this.person = null;
                this.lastIdentifiedPersonSample = null;
                this.NameText.Text = "Hi!";
                Debug.WriteLine("no face");
            }
            else
            {
               this.lastIdentifiedPersonSample = e.DetectedFaces.Select(f => new Tuple<Face, IdentifiedPerson>(f, e.IdentifiedPersons.FirstOrDefault(p => p.FaceId == f.FaceId)));
                this.person = e.IdentifiedPersons.FirstOrDefault();

                this.NameText.Text = "Welcome back, " + person.Person.Name;
                Debug.WriteLine("got face");
            }

            if (!e.SimilarFaceMatches.Any())
            {
                this.lastSimilarPersistedFaceSample = null;
            }
            else
            {
                this.lastSimilarPersistedFaceSample = e.SimilarFaceMatches;
            }
            
        }
        private async void ProcessingLoop()
        {
            while (this.isProcessingLoopInProgress)
            {
                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (!this.isProcessingPhoto && this.isEmotionResponseFlyoutOpened)
                    {
                        this.isProcessingPhoto = true;
                        if (this.cameraControl.NumFacesOnLastFrame == 0)
                        {
                            await ProcessCameraCapture(null);
                        }
                        else
                        {
                            await this.ProcessCameraCapture(await this.cameraControl.CaptureFrameAsync());
                        }
                    }
                });

                await Task.Delay(1000);
            }
        }

        private async Task ProcessCameraCapture(ImageAnalyzer e)
        {
            if (e == null)
            {
                this.isProcessingPhoto = false;
                return;
            }
            await e.DetectFacesAsync(detectFaceAttributes: true);
            // identify person 
            findIdendity(e);
            // detect emotions
            await e.DetectEmotionAsync();
        

            if (e.DetectedEmotion.Any())
            {
                // Update the average emotion response
                Scores averageScores = new Scores
                {
                    Happiness = e.DetectedEmotion.Average(em => em.Scores.Happiness),
                    Anger = e.DetectedEmotion.Average(em => em.Scores.Anger),
                    Sadness = e.DetectedEmotion.Average(em => em.Scores.Sadness),
                    Contempt = e.DetectedEmotion.Average(em => em.Scores.Contempt),
                    Disgust = e.DetectedEmotion.Average(em => em.Scores.Disgust),
                    Neutral = e.DetectedEmotion.Average(em => em.Scores.Neutral),
                    Fear = e.DetectedEmotion.Average(em => em.Scores.Fear),
                    Surprise = e.DetectedEmotion.Average(em => em.Scores.Surprise)
                };

                double positiveEmotionResponse = Math.Min(averageScores.Happiness + averageScores.Surprise, 1);
                double negativeEmotionResponse = Math.Min(averageScores.Sadness + averageScores.Fear + averageScores.Disgust + averageScores.Contempt, 1);
                double netResponse = ((positiveEmotionResponse - negativeEmotionResponse) * 0.5) + 0.5;

                this.sentimentControl.Sentiment = netResponse;
  
              //  handleReaction(netResponse);

                // show captured faces and their emotion
                if (this.emotionFacesGrid.Visibility == Visibility.Visible)
                {
                    foreach (var face in e.DetectedEmotion)
                    {
                        // Get top emotion on this face
                        EmotionData topEmotion = EmotionServiceHelper.ScoresToEmotionData(face.Scores).OrderByDescending(em => em.EmotionScore).First();

                        // Crop this face
                        Rectangle rect = face.FaceRectangle;
                        double heightScaleFactor = 1.8;
                        double widthScaleFactor = 1.8;
                        Rectangle biggerRectangle = new Rectangle
                        {
                            Height = Math.Min((int)(rect.Height * heightScaleFactor), e.DecodedImageHeight),
                            Width = Math.Min((int)(rect.Width * widthScaleFactor), e.DecodedImageWidth)
                        };
                        biggerRectangle.Left = Math.Max(0, rect.Left - (int)(rect.Width * ((widthScaleFactor - 1) / 2)));
                        biggerRectangle.Top = Math.Max(0, rect.Top - (int)(rect.Height * ((heightScaleFactor - 1) / 1.4)));

                        ImageSource croppedImage = await Util.GetCroppedBitmapAsync(e.GetImageStreamCallback, biggerRectangle);

                        // Add the face and emotion to the collection of faces
                        if (croppedImage != null && biggerRectangle.Height > 0 && biggerRectangle.Width > 0)
                        {
                            if (this.EmotionFaces.Count >= 9)
                            {
                                this.EmotionFaces.Clear();
                            }

                            this.EmotionFaces.Add(new EmotionExpressionCapture { CroppedFace = croppedImage, TopEmotion = topEmotion.EmotionName });
                        }
                    }
                }


            }

            this.isProcessingPhoto = false;
        }

        #endregion

        private void OnEmotionFacesToggleUnchecked(object sender, RoutedEventArgs e)
        {
            emotionFacesGrid.Visibility = Visibility.Collapsed;
        }

        private void OnEmotionFacesToggleChecked(object sender, RoutedEventArgs e)
        {
            emotionFacesGrid.Visibility = Visibility.Visible;
        }
        public Windows.UI.Color GetColorFromHex(string hexString)
        {
            hexString = hexString.Replace("#", string.Empty);
            byte r = byte.Parse(hexString.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hexString.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hexString.Substring(4, 2), NumberStyles.HexNumber);

            return Windows.UI.Color.FromArgb(byte.Parse("1"), r, g, b);
        }
    }

    public class EmotionExpressionCapture
    {
        public ImageSource CroppedFace { get; set; }
        public string TopEmotion { get; set; }
    }

    public class Item {
        public Item(string color, string url, List<string> keywords)
        {
            this.color = GetColorFromHex(color);
            Url = url;
            this.keywords = keywords;
        }

        public Windows.UI.Color color { get; set; }
        public string Url { get; set; }
        public List<String> keywords { get; set; }
        public Windows.UI.Color GetColorFromHex(string hexString)
        {
            hexString = hexString.Replace("#", string.Empty);
            byte r = byte.Parse(hexString.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hexString.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hexString.Substring(4, 2), NumberStyles.HexNumber);

            return Windows.UI.Color.FromArgb(byte.Parse("1"), r, g, b);
        }
    }



}


