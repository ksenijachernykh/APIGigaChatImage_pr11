using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace GigaChatImageGeneratorWPF
{
    public partial class MainWindow : Window
    {
        private const string ClientId = "019b2ccb-5c6b-7642-8aa5-70cc2538d43b";
        private const string AuthorizationKey = "MDE5YjJjY2ItNWM2Yi03NjQyLThhYTUtNzBjYzI1MzhkNDNiOjVhMTUwMjVmLTg4ZWMtNDNiZC05NjQxLWIxNzBiNmQ3NDM2Ng==";

        private List<Message> DialogHistory = new List<Message>();
        private string currentToken;

        public MainWindow()
        {
            InitializeComponent();
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) => true;

            GenerateButton.MouseEnter += (s, e) =>
            {
                GenerateButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            };

            GenerateButton.MouseLeave += (s, e) =>
            {
                GenerateButton.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            };
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                string prompt = PromptTextBox.Text.Trim();
                string style = (StyleComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Реализм";
                string palette = (PaletteComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Тёплая";
                string aspectRatio = (AspectRatioComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "16:9";

                
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MessageBox.Show("Введите описание изображения", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                
                UpdateUIForGeneration(true);

                
                currentToken = await GetTokenAsync();

                if (string.IsNullOrEmpty(currentToken))
                {
                    MessageBox.Show("Не удалось получить токен доступа", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateUIForGeneration(false);
                    return;
                }

                
                string extendedPrompt = FormatPromptWithParameters(prompt, style, palette, aspectRatio);

                
                DialogHistory.Add(new Message
                {
                    role = "user",
                    content = extendedPrompt
                });

                
                StatusTextBlock.Text = "Генерация изображения...";
                ResponseMessage answer = await GetAnswerAsync(currentToken, DialogHistory);

                if (answer == null || answer.choices == null || answer.choices.Count == 0)
                {
                    MessageBox.Show("Не удалось получить ответ от GigaChat", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateUIForGeneration(false);
                    return;
                }

                string assistantText = answer.choices[0].message.content;
                DialogHistory.Add(new Message
                {
                    role = "assistant",
                    content = assistantText
                });

                
                StatusTextBlock.Text = "Обработка ответа...";
                string fileId = ExtractImageId(assistantText);

                if (!string.IsNullOrEmpty(fileId))
                {
                    
                    StatusTextBlock.Text = "Скачивание изображения...";
                    byte[] imageData = await DownloadImageAsync(currentToken, fileId);

                    if (imageData != null && imageData.Length > 0)
                    {
                        
                        string imagePath = SaveImage(imageData, prompt);

                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            
                            StatusTextBlock.Text = "Установка обоев...";
                            SetWallpaper(imagePath);

                            
                            ShowSuccessMessage(imagePath, imageData.Length);
                        }
                        else
                        {
                            MessageBox.Show("Не удалось сохранить изображение", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Не удалось скачать изображение", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("В ответе не найдено изображение", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Ошибка сети: {ex.Message}", "Ошибка подключения",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ошибка сети";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Ошибка";
            }
            finally
            {
                UpdateUIForGeneration(false);
            }
        }

        private void UpdateUIForGeneration(bool isGenerating)
        {
            if (isGenerating)
            {
                GenerateButton.IsEnabled = false;
                GenerateButton.Content = "Генерация обоев...";
                StatusTextBlock.Text = "Начало генерации...";

                
                var ellipse = FindName("StatusEllipse") as Ellipse;
                if (ellipse != null)
                {
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); 
                }
            }
            else
            {
                GenerateButton.IsEnabled = true;
                GenerateButton.Content = "СГЕНЕРИРОВАТЬ И УСТАНОВИТЬ ОБОИ";
                StatusTextBlock.Text = "Готов к генерации";

                
                var ellipse = FindName("StatusEllipse") as Ellipse;
                if (ellipse != null)
                {
                    ellipse.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); 
                }
            }
        }

        private string FormatPromptWithParameters(string basePrompt, string style, string palette, string aspectRatio)
        {
            StringBuilder promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"Создай изображение для обоев рабочего стола: {basePrompt}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Параметры:");
            promptBuilder.AppendLine($"- Стиль: {style}");
            promptBuilder.AppendLine($"- Цветовая палитра: {palette}");
            promptBuilder.AppendLine($"- Соотношение сторон: {aspectRatio}");
            promptBuilder.AppendLine("- Высокое качество, детализированное");
            promptBuilder.AppendLine("- Без текста на изображении");
            promptBuilder.AppendLine("- Оптимизированное для обоев рабочего стола");

            return promptBuilder.ToString();
        }

        private void ShowSuccessMessage(string imagePath, long fileSize)
        {
            MessageBox.Show($"Обои успешно установлены!\n\n" +
                          $"Файл: {System.IO.Path.GetFileName(imagePath)}\n" +
                          $"Размер: {fileSize / 1024} КБ\n" +
                          $"Путь: {imagePath}",
                          "Готово!",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);

            StatusTextBlock.Text = "Обои установлены!";
        }

       
        private void SetWallpaper(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    MessageBox.Show($"Файл не найден: {imagePath}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                const int SPI_SETDESKWALLPAPER = 0x0014;
                const int SPIF_UPDATEINIFILE = 0x01;
                const int SPIF_SENDWININICHANGE = 0x02;

                [DllImport("user32.dll", CharSet = CharSet.Auto)]
                static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при установке обоев: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }


        private async Task<string> GetTokenAsync()
        {
            try
            {
                string url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("RqUID", ClientId);
                        request.Headers.Add("Authorization", $"Bearer {AuthorizationKey}");

                        var content = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                        });

                        request.Content = content;
                        var response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            ResponseToken tokenResponse = JsonConvert.DeserializeObject<ResponseToken>(responseContent);
                            return tokenResponse.access_token;
                        }
                        else
                        {
                            MessageBox.Show($"Ошибка при получении токена: {response.StatusCode}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения токена: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private async Task<ResponseMessage> GetAnswerAsync(string token, List<Message> messages)
        {
            try
            {
                string url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(120);

                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("Authorization", $"Bearer {token}");

                        var dataRequest = new Request
                        {
                            model = "GigaChat",
                            messages = messages,
                            function_call = "auto",
                            temperature = 0.7,
                            max_tokens = 1500
                        };

                        string jsonContent = JsonConvert.SerializeObject(dataRequest);
                        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                        var response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            return JsonConvert.DeserializeObject<ResponseMessage>(responseContent);
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Ошибка API: {errorContent}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Превышено время ожидания ответа", "Таймаут",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении ответа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private string ExtractImageId(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;

            try
            {
                var srcPattern = @"src\s*=\s*[""']([^""']+)[""']";
                var srcMatch = Regex.Match(content, srcPattern, RegexOptions.IgnoreCase);

                if (srcMatch.Success && srcMatch.Groups.Count > 1)
                {
                    string srcValue = srcMatch.Groups[1].Value;

                    if (srcValue.Contains("/files/"))
                    {
                        var parts = srcValue.Split(new[] { "/files/", "/content" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            return parts[1].TrimEnd('/');
                        }
                    }
                    return srcValue;
                }

                var fileIdPattern = @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}";
                var fileIdMatch = Regex.Match(content, fileIdPattern);

                if (fileIdMatch.Success)
                {
                    return fileIdMatch.Value;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при извлечении ID изображения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return null;
        }

        private async Task<byte[]> DownloadImageAsync(string token, string fileId)
        {
            try
            {
                string url = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(60);
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        var response = await client.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsByteArrayAsync();
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Ошибка скачивания: {errorContent}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании изображения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        private string SaveImage(byte[] imageData, string prompt)
        {
            try
            {
                string imagesFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Wallpapers");
                if (!Directory.Exists(imagesFolder))
                    Directory.CreateDirectory(imagesFolder);

                string safeFileName = Regex.Replace(prompt, @"[^\w\d]", "_");
                if (safeFileName.Length > 30)
                    safeFileName = safeFileName.Substring(0, 30);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"wallpaper_{safeFileName}_{timestamp}.jpg";
                string filePath = System.IO.Path.Combine(imagesFolder, fileName);

                File.WriteAllBytes(filePath, imageData);
                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изображения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"wallpaper_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string filePath = System.IO.Path.Combine(desktopPath, fileName);
                    File.WriteAllBytes(filePath, imageData);
                    return filePath;
                }
                catch
                {
                    return null;
                }
            }
        }
    }


    public class Request
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public string function_call { get; set; }
        public double temperature { get; set; }
        public int max_tokens { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class ResponseMessage
    {
        public List<Choice> choices { get; set; }
        public int created { get; set; }
        public string model { get; set; }
        public string @object { get; set; }
        public Usage usage { get; set; }

        public class Usage
        {
            public int completion_tokens { get; set; }
            public int prompt_tokens { get; set; }
            public int system_tokens { get; set; }
            public int total_tokens { get; set; }
        }

        public class Choice
        {
            public string finish_reason { get; set; }
            public int index { get; set; }
            public ResponseMessageContent message { get; set; }
        }

        public class ResponseMessageContent
        {
            public string role { get; set; }
            public string content { get; set; }
            public string functions_state_id { get; set; }
        }
    }

    public class ResponseToken
    {
        public string access_token { get; set; }
        public string expires_at { get; set; }
    }
}