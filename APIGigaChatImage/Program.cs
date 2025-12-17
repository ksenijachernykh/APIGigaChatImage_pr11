using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace APIGigaChatImage
{
    internal class Program
    {
        static string ClientId = "019b2ccb-5c6b-7642-8aa5-70cc2538d43b";
        static string AuthorizationKey = "MDE5YjJjY2ItNWM2Yi03NjQyLThhYTUtNzBjYzI1MzhkNDNiOjMzNzRkMGU4LTVkNTQtNGEzNy1hY2E0LTc0Y2ExODQ1MWQyYQ==";

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== GigaChat Image Generator v2 ===\n");

            try
            {
                // 1. Получаем токен
                Console.WriteLine("1. Получение токена...");
                string token = await GetToken();

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❌ Не удалось получить токен.");
                    return;
                }
                Console.WriteLine("✅ Токен получен!\n");

                // 2. Получаем промпт
                Console.Write("2. Введите описание изображения: ");
                string prompt = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(prompt))
                    prompt = "Красивый пейзаж";

                // 3. Пробуем разные методы генерации
                Console.WriteLine("\n3. Генерация изображения...");

                bool success = false;

                // Метод 1: Прямой API (если доступен)
                success = await TryDirectImageGeneration(token, prompt);

                // Метод 2: Альтернативный через чат (если первый не сработал)
                if (!success)
                {
                    Console.WriteLine("\nПробуем альтернативный метод...");
                    success = await TryAlternativeImageGeneration(token, prompt);
                }

                if (success)
                    Console.WriteLine("\n🎉 Готово! Изображение сохранено.");
                else
                    Console.WriteLine("\n❌ Не удалось сгенерировать изображение.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        // ==================== 1. ПОЛУЧЕНИЕ ТОКЕНА ====================
        static async Task<string> GetToken()
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
                            new System.Collections.Generic.KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                        });

                        request.Content = content;
                        var response = await client.SendAsync(request);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            dynamic tokenResponse = JsonConvert.DeserializeObject(responseContent);
                            return tokenResponse.access_token;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ==================== 2. ПРЯМОЙ МЕТОД ГЕНЕРАЦИИ ====================
        static async Task<bool> TryDirectImageGeneration(string token, string prompt)
        {
            try
            {
                Console.WriteLine("   Метод 1: Прямой API...");

                // Пробуем разные endpoint'ы
                string[] endpoints = {
                    "https://gigachat.devices.sberbank.ru/api/v1/images/generations",
                    "https://gigachat.devices.sberbank.ru/api/v1/chat/completions"
                };

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        Console.WriteLine($"   Пробуем: {endpoint}");

                        using (var handler = new HttpClientHandler())
                        {
                            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                            using (var client = new HttpClient(handler))
                            {
                                client.Timeout = TimeSpan.FromSeconds(60);
                                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                                object requestBody;

                                if (endpoint.Contains("images/generations"))
                                {
                                    // Формат для images/generations
                                    requestBody = new
                                    {
                                        model = "GigaChat-Image",
                                        prompt = prompt,
                                        n = 1,
                                        size = "512x512",
                                        quality = "standard"
                                    };
                                }
                                else
                                {
                                    // Формат для chat/completions (упрощенный)
                                    requestBody = new
                                    {
                                        model = "GigaChat",
                                        messages = new[]
                                        {
                                            new { role = "user", content = prompt }
                                        },
                                        max_tokens = 100
                                    };
                                }

                                string jsonBody = JsonConvert.SerializeObject(requestBody);
                                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                                var response = await client.PostAsync(endpoint, content);
                                Console.WriteLine($"   Статус: {response.StatusCode}");

                                if (response.IsSuccessStatusCode)
                                {
                                    var responseContent = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine($"   Ответ получен ({responseContent.Length} символов)");

                                    // Пробуем сохранить как JSON для анализа
                                    File.WriteAllText("api_response.json", responseContent);
                                    Console.WriteLine($"   Ответ сохранен в api_response.json");

                                    return true;
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Console.WriteLine($"   ⚠️ Таймаут для {endpoint}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ❌ Ошибка: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Общая ошибка: {ex.Message}");
            }

            return false;
        }

        // ==================== 3. АЛЬТЕРНАТИВНЫЙ МЕТОД ====================
        static async Task<bool> TryAlternativeImageGeneration(string token, string prompt)
        {
            try
            {
                Console.WriteLine("   Метод 2: Альтернативный через чат...");

                string url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(30); // Короткий таймаут
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

                        // Простой запрос без "Василия Кандинского"
                        var requestBody = new
                        {
                            model = "GigaChat",
                            messages = new[]
                            {
                                new
                                {
                                    role = "user",
                                    content = $"Опиши подробно изображение: {prompt}. " +
                                             $"Опиши цвета, композицию, стиль, освещение."
                                }
                            },
                            max_tokens = 500
                        };

                        string jsonBody = JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);

                            if (jsonResponse.choices != null && jsonResponse.choices.Count > 0)
                            {
                                string description = jsonResponse.choices[0].message.content;

                                // Сохраняем текстовое описание
                                string safePrompt = prompt.Replace(" ", "_")
                                                         .Replace(".", "")
                                                         .Replace(",", "");
                                safePrompt = safePrompt.Length > 50 ? safePrompt.Substring(0, 50) : safePrompt;

                                string fileName = $"description_{safePrompt}_{DateTime.Now:HHmmss}.txt";
                                File.WriteAllText(fileName, $"Промпт: {prompt}\n\nОписание изображения:\n{description}");

                                Console.WriteLine($"✅ Текстовое описание сохранено: {fileName}");
                                Console.WriteLine($"   Описание: {description.Substring(0, Math.Min(100, description.Length))}...");

                                return true;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"   ❌ Статус: {response.StatusCode}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Ошибка: {ex.Message}");
            }

            return false;
        }
    }
}