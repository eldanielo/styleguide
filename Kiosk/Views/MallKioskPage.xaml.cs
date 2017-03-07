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
        private string basketurl = "https://www.swarovski.com/is-bin/INTERSHOP.enfinity/WFS/SCO-Web_AT-Site/de_DE/-/EUR/ViewData-Start/1232332531?JumpTarget=ViewRequisitionCheckout-ShowLoginPage&=&=&=";
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

        private void handleReaction(double sentiment) {
            Item recommendation = null;
            if (this.currentRecommendation != null)
            {
                if (sentiment <= 0.33)
                {
                    recommendation = getRecommandation(currentTarget);
                    // look for an override for negative sentiment
                    // behaviorAction = this.currentRecommendation.SpeechSentimentBehavior.FirstOrDefault(behavior => string.Compare(behavior.Key, "Negative", true) == 0);
                }
                else if (sentiment >= 0.66)
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
            this.imageFromCameraWithFaces.DataContext = e;
            this.imageFromCameraWithFaces.Visibility = Visibility.Visible;

            // We induce a delay here to give the captured image some time to render before we hide the camera.
            // This avoids a black flash.
            await Task.Delay(50);

            await this.cameraControl.StopStreamAsync();
            await e.AnalyseAsync();
            currentTarget = e;
            ShowRecommendations(e);

            //e.FaceRecognitionCompleted += (s, args) =>
            //{
            //    ShowRecommendations(e);
            //};

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
                     new Item("#FFFFFF", "http://www.swarovski.com/Web_AT/de/1121080/product/Alana_Ohrstecker.html", null),
                      new Item("#61616A", "http://www.swarovski.com/Web_AT/de/5271718/product/Crystaldust_Kreolen,_klein,_schwarz.html", null)
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
        private void ShowRecommendations(ImageAnalyzer image)
        {
            Item recommendation = getRecommandation(image);
            if (recommendation != null)
            {
                webView.Navigate(new Uri(recommendation.Url));
                webView.Visibility = Visibility.Visible;
                this.currentRecommendation = recommendation;
            }
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

//                handleReaction(netResponse);

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
