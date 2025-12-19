using APIGigaChatImage.Classes;
using APIGigaChatImage.Models.Request;
using APIGigaChatImage.Models.Response;
using GigaChatWPF.Models;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Formatting = Newtonsoft.Json.Formatting;

namespace GigaChatWPF
{
    public partial class MainWindow : Window
    {
        private readonly PromptBuilder _promptBuilder;
        private bool _isGenerating = false;
        private const string CustomStylePlaceholder = "Свой вариант...";

        // Данные из консольного приложения
        private static string _token;
        private static List<Message> _dialogHistory = new List<Message>();
        private const string ClientId = "019b2ccb-5c6b-7642-8aa5-70cc2538d43b";
        private const string AuthorizationKey = "MDE5YjJjY2ItNWM2Yi03NjQyLThhYTUtNzBjYzI1MzhkNDNiOmMzYjFiMzYzLWM3YzItNDhlYi04MjMzLTZhMzhiMDczZDI0MA==";

        public MainWindow()
        {
            InitializeComponent();
            _promptBuilder = new PromptBuilder();

            // Привязка событий
            DetailSlider.ValueChanged += DetailSlider_ValueChanged;
            PreviewButton.Click += PreviewButton_Click;
            GenerateButton.Click += GenerateButton_Click;
            IncludeTextCheckBox.Checked += IncludeTextCheckBox_Changed;
            IncludeTextCheckBox.Unchecked += IncludeTextCheckBox_Changed;
            CustomStyleTextBox.GotFocus += CustomStyleTextBox_GotFocus;
            CustomStyleTextBox.LostFocus += CustomStyleTextBox_LostFocus;

            // Обновление предпросмотра при изменении параметров
            PromptTextBox.TextChanged += UpdatePreview;
            StyleComboBox.SelectionChanged += UpdatePreview;
            CustomStyleTextBox.TextChanged += UpdatePreview;
            DetailSlider.ValueChanged += UpdatePreview;
            WarmColorsCheckBox.Checked += UpdatePreview;
            WarmColorsCheckBox.Unchecked += UpdatePreview;
            ColdColorsCheckBox.Checked += UpdatePreview;
            ColdColorsCheckBox.Unchecked += UpdatePreview;
            PastelColorsCheckBox.Checked += UpdatePreview;
            PastelColorsCheckBox.Unchecked += UpdatePreview;
            BrightColorsCheckBox.Checked += UpdatePreview;
            BrightColorsCheckBox.Unchecked += UpdatePreview;
            Ratio16_9.Checked += UpdatePreview;
            Ratio4_3.Checked += UpdatePreview;
            Ratio1_1.Checked += UpdatePreview;
            Ratio9_16.Checked += UpdatePreview;
            MoodCalm.Checked += UpdatePreview;
            MoodEpic.Checked += UpdatePreview;
            MoodFuturistic.Checked += UpdatePreview;

            // Инициализация
            UpdatePreview(null, null);
        }

        private void DetailSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DetailValueText.Text = ((int)e.NewValue).ToString();
        }

        private void IncludeTextCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            TextContentGrid.Visibility = IncludeTextCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdatePreview(sender, e);
        }

        private void CustomStyleTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (CustomStyleTextBox.Text == CustomStylePlaceholder)
            {
                CustomStyleTextBox.Text = "";
                CustomStyleTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void CustomStyleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CustomStyleTextBox.Text))
            {
                CustomStyleTextBox.Text = CustomStylePlaceholder;
                CustomStyleTextBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void UpdatePreview(object sender, RoutedEventArgs e)
        {
            try
            {
                string finalPrompt = _promptBuilder.BuildPrompt(
                    PromptTextBox.Text,
                    GetSelectedStyle(),
                    GetSelectedColors(),
                    GetSelectedAspectRatio(),
                    GetSelectedMood(),
                    (int)DetailSlider.Value,
                    IncludeTextCheckBox.IsChecked == true ? TextContentTextBox.Text : null
                );

                PreviewTextBox.Text = finalPrompt;
            }
            catch (Exception ex)
            {
                PreviewTextBox.Text = $"Ошибка формирования запроса: {ex.Message}";
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdatePreview(null, null);
                MessageBox.Show("Запрос сформирован! Проверьте поле 'Предпросмотр запроса'.",
                    "Предпросмотр",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isGenerating)
            {
                MessageBox.Show("Пожалуйста, дождитесь завершения текущей генерации.",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                MessageBox.Show("Введите основную идею для генерации изображения.",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _isGenerating = true;
                GenerateButton.IsEnabled = false;
                PreviewButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.IsIndeterminate = true;
                StatusText.Text = "Начало генерации...";

                // Формируем финальный промпт
                string finalPrompt = PreviewTextBox.Text;
                StatusText.Text = "Получение токена...";

                // Получаем токен
                if (string.IsNullOrEmpty(_token))
                {
                    _token = await GetTokenAsync();
                    if (string.IsNullOrEmpty(_token))
                    {
                        MessageBox.Show("Не удалось получить токен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                StatusText.Text = "Отправка запроса GigaChat...";

                // Добавляем запрос в историю
                _dialogHistory.Add(new Message
                {
                    role = "user",
                    content = finalPrompt
                });

                // Получаем ответ от GigaChat
                ResponseMessage answer = await GetAnswerAsync(_token, _dialogHistory);

                if (answer == null || answer.choices == null || answer.choices.Count == 0)
                {
                    MessageBox.Show("Не удалось получить ответ от GigaChat.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string assistantText = answer.choices[0].message.content;
                _dialogHistory.Add(new Message
                {
                    role = "assistant",
                    content = assistantText
                });

                // Извлекаем ID изображения
                string fileId = ExtractImageId(assistantText);

                if (string.IsNullOrEmpty(fileId))
                {
                    MessageBox.Show("Ответ не содержит изображения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StatusText.Text = $"Найден ID изображения: {fileId}\nСкачивание...";

                // Скачиваем изображение
                byte[] imageData = await DownloadImageAsync(_token, fileId);

                if (imageData == null || imageData.Length == 0)
                {
                    MessageBox.Show("Не удалось скачать изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusText.Text = "Сохранение изображения...";

                // Сохраняем изображение
                string imagePath = SaveImage(imageData, PromptTextBox.Text);

                if (string.IsNullOrEmpty(imagePath))
                {
                    MessageBox.Show("Не удалось сохранить изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusText.Text = "Установка обоев...";

                // Устанавливаем обои
                try
                {
                    WallpaperSetter.SetWallpaper(imagePath);
                    SaveDialogHistory();

                    MessageBox.Show($"✅ Обои успешно сгенерированы и установлены!\n\n" +
                                  $"Файл: {Path.GetFileName(imagePath)}\n" +
                                  $"Размер: {imageData.Length / 1024} КБ\n" +
                                  $"Путь: {imagePath}",
                                  "Успех",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                    StatusText.Text = "Готово! Обои установлены.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Изображение сохранено, но не удалось установить обои.\n\n" +
                                  $"Ошибка: {ex.Message}\n\n" +
                                  $"Файл сохранён: {imagePath}\n" +
                                  $"Вы можете установить обои вручную.",
                                  "Внимание",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    StatusText.Text = "Изображение сохранено.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusText.Text = "Ошибка.";
            }
            finally
            {
                _isGenerating = false;
                GenerateButton.IsEnabled = true;
                PreviewButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
                ProgressBar.IsIndeterminate = false;
            }
        }

        #region Методы из консольного приложения

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
                            Console.WriteLine($"Ошибка получения токена: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при получении токена: {ex.Message}");
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
                        Console.WriteLine($"Статус ответа: {response.StatusCode}");

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            ResponseMessage responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(responseContent);
                            return responseMessage;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ошибка от API: {errorContent}");
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Таймаут при получении ответа.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при получении ответа: {ex.Message}");
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
                    Console.WriteLine($"Найден src: {srcValue}");

                    if (srcValue.Contains("/files/"))
                    {
                        var parts = srcValue.Split(new[] { "/files/", "/content" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string fileId = parts[1].TrimEnd('/');
                            return fileId;
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
                Console.WriteLine($"Ошибка при извлечении ID изображения: {ex.Message}");
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

                        Console.WriteLine($"Статус скачивания: {response.StatusCode}");

                        if (response.IsSuccessStatusCode)
                        {
                            byte[] imageData = await response.Content.ReadAsByteArrayAsync();
                            Console.WriteLine($"Изображение скачано: {imageData.Length} байт");
                            return imageData;
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"Ошибка скачивания: {errorContent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при скачивании изображения: {ex.Message}");
            }

            return null;
        }

        private string SaveImage(byte[] imageData, string prompt)
        {
            try
            {
                string imagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeneratedImages");
                if (!Directory.Exists(imagesFolder))
                    Directory.CreateDirectory(imagesFolder);

                string safeFileName = GenerateSafeFileName(prompt);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"image_{safeFileName}_{timestamp}.jpg";
                string filePath = Path.Combine(imagesFolder, fileName);

                File.WriteAllBytes(filePath, imageData);
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении изображения: {ex.Message}");

                try
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fileName = $"gigachat_image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string filePath = Path.Combine(desktopPath, fileName);
                    File.WriteAllBytes(filePath, imageData);
                    return filePath;
                }
                catch
                {
                    return null;
                }
            }
        }

        private void SaveDialogHistory()
        {
            try
            {
                string historyFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DialogHistory");
                if (!Directory.Exists(historyFolder))
                    Directory.CreateDirectory(historyFolder);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filePath = Path.Combine(historyFolder, $"dialog_{timestamp}.json");

                var historyData = new
                {
                    timestamp = DateTime.Now,
                    messages = _dialogHistory,
                    total_messages = _dialogHistory.Count
                };

                string jsonContent = JsonConvert.SerializeObject(historyData, Formatting.Indented);
                File.WriteAllText(filePath, jsonContent);

                Console.WriteLine($"История диалога сохранена: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении истории: {ex.Message}");
            }
        }

        private string GenerateSafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            string safe = Regex.Replace(input, @"[^\w\d]", "_");

            if (safe.Length > 50)
                safe = safe.Substring(0, 50);

            return safe;
        }

        #endregion

        #region Вспомогательные методы для получения параметров

        private string GetSelectedStyle()
        {
            string customText = CustomStyleTextBox.Text;
            if (!string.IsNullOrWhiteSpace(customText) && customText != CustomStylePlaceholder)
                return customText;

            if (StyleComboBox.SelectedItem is ComboBoxItem selectedItem)
                return selectedItem.Content.ToString();

            return "Реализм";
        }

        private List<string> GetSelectedColors()
        {
            var colors = new List<string>();

            if (WarmColorsCheckBox.IsChecked == true) colors.Add("тёплые цвета");
            if (ColdColorsCheckBox.IsChecked == true) colors.Add("холодные цвета");
            if (PastelColorsCheckBox.IsChecked == true) colors.Add("пастельные тона");
            if (BrightColorsCheckBox.IsChecked == true) colors.Add("яркие цвета");

            return colors;
        }

        private string GetSelectedAspectRatio()
        {
            if (Ratio16_9.IsChecked == true) return "16:9";
            if (Ratio4_3.IsChecked == true) return "4:3";
            if (Ratio1_1.IsChecked == true) return "1:1";
            if (Ratio9_16.IsChecked == true) return "9:16";

            return "16:9";
        }

        private string GetSelectedMood()
        {
            if (MoodCalm.IsChecked == true) return "спокойное";
            if (MoodEpic.IsChecked == true) return "эпическое";
            if (MoodFuturistic.IsChecked == true) return "футуристическое";

            return "эпическое";
        }

        #endregion
    }
}