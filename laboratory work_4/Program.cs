using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // Нужно для лечения консоли
using System.Text;

namespace WordAnagramFinder
{
    class Program
    {
        // Специальное исправление для старых консолей Windows (лечит ввод русских букв)
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCP(uint wCodePageID);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        private class DictionaryWord
        {
            public string Word { get; set; }
            public int[] LetterCounts { get; set; }
            public int Length => Word.Length;
        }

        private static List<DictionaryWord> _processedDictionary = new List<DictionaryWord>();

        static void Main(string[] args)
        {
            // Принудительно ставим UTF-8 (65001) на уровне операционной системы для этого окна
            SetConsoleCP(65001);
            SetConsoleOutputCP(65001);
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string dictionaryPath = "russian_nouns.txt";

            if (!File.Exists(dictionaryPath))
            {
                Console.WriteLine($"Ошибка: Файл словаря '{dictionaryPath}' не найден!");
                return;
            }

            Console.WriteLine("Инициализация словаря... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            InitializeDictionary(dictionaryPath);
            watch.Stop();

            Console.WriteLine($"Словарь успешно загружен! Загружено слов: {_processedDictionary.Count}");
            Console.WriteLine($"Время инициализации: {watch.Elapsed.TotalSeconds:F2} сек.\n");

            // --- БЛОК ПРОВЕРКИ СЛОВАРЯ ---
            if (_processedDictionary.Count >= 6)
            {
                Console.WriteLine("=== ПРОВЕРКА ЗАГРУЗКИ СЛОВАРЯ ===");
                Console.WriteLine("Первые 3 слова:");
                for (int i = 0; i < 3; i++)
                    Console.WriteLine($"  [{i + 1}] {_processedDictionary[i].Word}");

                Console.WriteLine("Последние 3 слова:");
                for (int i = _processedDictionary.Count - 3; i < _processedDictionary.Count; i++)
                    Console.WriteLine($"  [{i + 1}] {_processedDictionary[i].Word}");
                Console.WriteLine("=================================\n");
            }
            // -----------------------------

            while (true)
            {
                Console.Write("Введите слово (или нажмите Enter для выхода): ");

                // Читаем строку посимвольно, чтобы обойти баг длинного буфера консоли
                StringBuilder inputBuilder = new StringBuilder();
                while (true)
                {
                    var keyInfo = Console.ReadKey(intercept: true); // intercept: true скрывает символ, мы выведем его сами
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine(); // Перенос строки при нажатии Enter
                        break;
                    }
                    // ИСПРАВЛЕНО: ConsoleKey.Backspace вместо Backend
                    if (keyInfo.Key == ConsoleKey.Backspace && inputBuilder.Length > 0)
                    {
                        // Поддержка удаления символа (Backspace)
                        inputBuilder.Remove(inputBuilder.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (keyInfo.KeyChar != '\0' && keyInfo.Key != ConsoleKey.Backspace)
                    {
                        inputBuilder.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar); // Отображаем символ на экране
                    }
                }

                string input = inputBuilder.ToString();
                if (string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
                    break;

                Console.WriteLine($"\n[Отладка] Консоль успешно прочитала: {input}");

                string cleanedInput = CleanWord(input);

                if (string.IsNullOrEmpty(cleanedInput))
                {
                    Console.WriteLine("Ошибка: Введенное слово не содержит русских букв!");
                    continue;
                }

                Console.WriteLine("Поиск подходящих слов...");
                watch.Restart();
                var results = FindWordsFromLetters(cleanedInput);
                watch.Stop();

                if (results.Count == 0)
                {
                    Console.WriteLine("Из букв этого слова нельзя составить ни одного слова из словаря.");
                }
                else
                {
                    Console.WriteLine($"\nНайдено слов: {results.Count} (Время поиска: {watch.Elapsed.TotalMilliseconds:F2} мс):");
                    foreach (var word in results)
                    {
                        Console.WriteLine($"{word} (длина: {word.Length})");
                    }
                }
                Console.WriteLine(new string('-', 40));
            }
        }

        // Остальные методы (InitializeDictionary, FindWordsFromLetters, CanFormWord, CleanWord, GetLetterCounts) 
        // оставьте точно такими же, какими они были в предыдущем ответе.
        private static void InitializeDictionary(string filePath)
        {
            string allText = File.ReadAllText(filePath, Encoding.UTF8).Replace("\uFEFF", "");
            string[] rawWords = allText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawWord in rawWords)
            {
                string word = CleanWord(rawWord);
                if (string.IsNullOrEmpty(word) || word.Length < 2) continue;
                _processedDictionary.Add(new DictionaryWord { Word = word, LetterCounts = GetLetterCounts(word) });
            }
        }

        private static List<string> FindWordsFromLetters(string inputWord)
        {
            int[] inputCounts = GetLetterCounts(inputWord);
            List<string> foundWords = new List<string>();
            foreach (var target in _processedDictionary)
            {
                if (target.Length > inputWord.Length) continue;
                if (CanFormWord(target.LetterCounts, inputCounts)) foundWords.Add(target.Word);
            }
            return foundWords.OrderByDescending(w => w.Length).ThenBy(w => w).ToList();
        }

        private static bool CanFormWord(int[] targetCounts, int[] availableCounts)
        {
            for (int i = 0; i < 33; i++) { if (targetCounts[i] > availableCounts[i]) return false; }
            return true;
        }

        private static string CleanWord(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            StringBuilder sb = new StringBuilder();
            string lower = raw.Trim().ToLower();
            foreach (char c in lower) { if ((c >= 'а' && c <= 'я') || c == 'ё') sb.Append(c); }
            return sb.ToString();
        }

        private static int[] GetLetterCounts(string word)
        {
            int[] counts = new int[33];
            foreach (char c in word)
            {
                char letter = c;
                if (letter == 'ё') letter = 'е';
                if (letter >= 'а' && letter <= 'я') counts[letter - 'а']++;
            }
            return counts;
        }
    }
}