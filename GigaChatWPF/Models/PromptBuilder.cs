using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GigaChatWPF.Models
{
    public class PromptBuilder
    {
        public string BuildPrompt(
            string mainPrompt,
            string style,
            List<string> colors,
            string aspectRatio,
            string mood,
            int detailLevel,
            string includedText = null)
        {
            var promptParts = new List<string>();

            // Основной запрос
            promptParts.Add(mainPrompt);

            // Стиль
            if (!string.IsNullOrWhiteSpace(style) && style != "Свой вариант...")
            {
                promptParts.Add($"в стиле {style.ToLower()}");
            }

            // Цветовая палитра
            if (colors.Any())
            {
                promptParts.Add($"цветовая палитра: {string.Join(", ", colors)}");
            }

            // Соотношение сторон
            if (!string.IsNullOrWhiteSpace(aspectRatio))
            {
                promptParts.Add($"соотношение сторон {aspectRatio}");
            }

            // Настроение
            if (!string.IsNullOrWhiteSpace(mood))
            {
                promptParts.Add($"{mood} настроение");
            }

            // Уровень детализации
            if (detailLevel > 5)
            {
                promptParts.Add($"высокий уровень детализации ({detailLevel}/10)");
            }
            else if (detailLevel < 5)
            {
                promptParts.Add($"минималистичная детализация ({detailLevel}/10)");
            }

            // Текст
            if (!string.IsNullOrWhiteSpace(includedText))
            {
                promptParts.Add($"включить текст: '{includedText}'");
            }

            // Ключевые слова для обоев
            promptParts.Add("обои рабочего стола, высокое качество, профессиональная цифровая живопись");

            // Формируем финальный промпт
            StringBuilder finalPrompt = new StringBuilder();
            finalPrompt.Append("Создай изображение для обоев рабочего стола: ");
            finalPrompt.Append(string.Join(", ", promptParts));
            finalPrompt.Append(". Изображение должно быть в высоком разрешении.");

            return finalPrompt.ToString();
        }
    }
}
