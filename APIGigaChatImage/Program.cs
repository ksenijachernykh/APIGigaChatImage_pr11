using APIGigaChatImage.Classes;
using APIGigaChatImage.Models.Request;
using APIGigaChatImage.Models.Response;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace APIGigaChatImage
{
    internal class Program
    {
        static string ClientId = "019b2ccb-5c6b-7642-8aa5-70cc2538d43b";
        static string AuthorizationKey = "MDE5YjJjY2ItNWM2Yi03NjQyLThhYTUtNzBjYzI1MzhkNDNiOjVhMTUwMjVmLTg4ZWMtNDNiZC05NjQxLWIxNzBiNmQ3NDM2Ng==";

        static List<Message> DialogHistory = new List<Message>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("GigaChat Image Generator\n");

            try
            {
                Console.WriteLine("1.Получение токена...");
                string token = await GetToken(ClientId, AuthorizationKey);

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Не удалось получить токен.");
                    return;
                }
                Console.WriteLine("Токен получен!\n");

                while (true)
                {
                    Console.Write("\nВведите запрос или 'выход' для завершения: ");
                    string userMessage = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(userMessage))
                        continue;

                    if (userMessage.ToLower() == "выход" || userMessage.ToLower() == "exit")
                        break;

                    DialogHistory.Add(new Message
                    {
                        role = "user",
                        content = userMessage
                    });

                    Console.WriteLine("\nГенерация ответа...");
                    ResponseMessage answer = await GetAnswer(token, DialogHistory);

                    if (answer == null || answer.choices == null || answer.choices.Count == 0)
                    {
                        Console.WriteLine("Не удалось получить ответ от GigaChat.");
                        continue;
                    }

                    string assistantText = answer.choices[0].message.content;
                    Console.WriteLine($"\nОтвет: {assistantText}");

                    DialogHistory.Add(new Message
                    {
                        role = "assistant",
                        content = assistantText
                    });

                    string fileId = ExtractImageId(assistantText);

                    if (!string.IsNullOrEmpty(fileId))
                    {
                        Console.WriteLine($"\nНайден ID изображения: {fileId}");
                        Console.WriteLine("Скачивание изображения...");

                        byte[] imageData = await DownloadImage(token, fileId);

                        if (imageData != null && imageData.Length > 0)
                        {
                            string imagePath = SaveImage(imageData, userMessage);
                            Console.WriteLine($"Изображение сохранено: {imagePath}");

                            Console.WriteLine("\nУстановка обоев рабочего стола...");
                            try
                            {
                                WallpaperSetter.SetWallpaper(imagePath);
                                Console.WriteLine($"Обои успешно установлены!");

                                Console.WriteLine($"Файл: {Path.GetFileName(imagePath)}");
                                Console.WriteLine($"Размер: {imageData.Length / 1024} КБ");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при установке обоев: {ex.Message}");

                                Console.WriteLine($"\nВы можете установить обои вручную:");
                                Console.WriteLine($"1.Откройте папку: {Path.GetDirectoryName(imagePath)}");
                                Console.WriteLine($"2.Правой кнопкой мыши по файлу '{Path.GetFileName(imagePath)}'");
                                Console.WriteLine($"3.Выберите 'Сделать фоновым изображением рабочего стола'");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Не удалось скачать изображение.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Ответ не содержит изображения.");
                    }
                    SaveDialogHistory();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nКритическая ошибка: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nПрограмма завершена. Нажмите любую клавишу...");
            Console.ReadKey();
        }

        static async Task<string> GetToken(string clientId, string authorizationKey)
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
                        request.Headers.Add("RqUID", clientId);
                        request.Headers.Add("Authorization", $"Bearer {authorizationKey}");

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

        static async Task<ResponseMessage> GetAnswer(string token, List<Message> messages)
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

        static string ExtractImageId(string content)
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

        static async Task<byte[]> DownloadImage(string token, string fileId)
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

        static string SaveImage(byte[] imageData, string prompt)
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

        static void SaveDialogHistory()
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
                    messages = DialogHistory,
                    total_messages = DialogHistory.Count
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

        static string GenerateSafeFileName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "unknown";

            string safe = Regex.Replace(input, @"[^\w\d]", "_");

            if (safe.Length > 50)
                safe = safe.Substring(0, 50);

            return safe;
        }
    }
}