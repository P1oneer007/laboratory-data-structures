using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WordAnagramFinder
{
    class Program
    {
        // Структура для быстрого сопоставления слов
        private class DictionaryWord
        {
            public string Word { get; set; }
            public int[] LetterCounts { get; set; } // Массив частот букв (на 34 элемента)
            public int Length => Word.Length;
        }

        private static List<DictionaryWord> _processedDictionary = new List<DictionaryWord>();

        static void Main(string[] args)
        {
            // Настройка консоли на работу с UTF-8
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string dictionaryPath = "russian_nouns.txt"; // Укажите имя вашего файла (txt или json)

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

            while (true)
            {
                Console.Write("Введите слово (или нажмите Enter для выхода): ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                    break;

                Console.WriteLine("Поиск подходящих слов...");
                watch.Restart();

                var results = FindWordsFromLetters(input);

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

        private static void InitializeDictionary(string filePath)
        {
            // Читаем все строки
            string[] rawWords = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var rawWord in rawWords)
            {
                // Важно: убираем пробелы, невидимые \r, \n и знаки ударения, если они есть в Harrix-словарe
                string word = rawWord.Replace("\r", "").Replace("\n", "").Trim().ToLower();

                if (string.IsNullOrEmpty(word)) continue;

                _processedDictionary.Add(new DictionaryWord
                {
                    Word = word,
                    LetterCounts = GetLetterCounts(word)
                });
            }
        }

        private static List<string> FindWordsFromLetters(string inputWord)
        {
            int[] inputCounts = GetLetterCounts(inputWord);
            List<string> foundWords = new List<string>();

            foreach (var target in _processedDictionary)
            {
                // Оптимизация: если слово в словаре длиннее введённого, пропускаем
                if (target.Length > inputWord.Length)
                    continue;

                if (CanFormWord(target.LetterCounts, inputCounts))
                {
                    foundWords.Add(target.Word);
                }
            }

            // Сортировка по уменьшению длины, а затем по алфавиту
            return foundWords
                .OrderByDescending(w => w.Length)
                .ThenBy(w => w)
                .ToList();
        }

        private static bool CanFormWord(int[] targetCounts, int[] availableCounts)
        {
            for (int i = 0; i < 34; i++) // Проверяем все 34 индекса
            {
                if (targetCounts[i] > availableCounts[i])
                    return false;
            }
            return true;
        }

        private static int[] GetLetterCounts(string word)
        {
            int[] counts = new int[34]; // Размер 34: 33 для 'а'-'я' + 1 для 'ё'
            foreach (char c in word)
            {
                if (c >= 'а' && c <= 'я')
                {
                    counts[c - 'а']++;
                }
                else if (c == 'ё')
                {
                    counts[33]++; // Буква 'ё' записывается в последнюю ячейку
                }
            }
            return counts;
        }
    }
}

/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WordAnagramFinder
{
    class Program
    {
        // Структура для быстрого сопоставления слов
        private class DictionaryWord
        {
            public string Word { get; set; }
            public int[] LetterCounts { get; set; } // Массив частот букв (для русского алфавита)
            public int Length => Word.Length;
        }

        private static List<DictionaryWord> _processedDictionary = new List<DictionaryWord>();

        static void Main(string[] args)
        {
            // Настройка консоли на работу с UTF-8
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // 1. ЭТАП ИНИЦИАЛИЗАЦИИ И ОБРАБОТКИ СЛОВАРЯ
            string dictionaryPath = "russian_nouns.txt"; // Путь к словарю из Harrix/Russian-Nouns

            if (!File.Exists(dictionaryPath))
            {
                Console.WriteLine($"Ошибка: Файл словаря '{dictionaryPath}' не найден!");
                Console.WriteLine("Пожалуйста, положите файл со словами в папку с программой.");
                return;
            }

            Console.WriteLine("Инициализация словаря... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            InitializeDictionary(dictionaryPath);

            watch.Stop();
            Console.WriteLine($"Словарь успешно загружен! Загружено слов: {_processedDictionary.Count}");
            Console.WriteLine($"Время инициализации: {watch.Elapsed.TotalSeconds:F2} сек.\n");

            // 2. ЭТАП ОБРАБОТКИ ЗАПРОСОВ (Основной цикл работы)
            while (true)
            {
                Console.Write("Введите слово (или нажмите Enter для выхода): ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                    break;

                Console.WriteLine("Поиск подходящих слов...");
                watch.Restart();

                // Поиск и сортировка по убыванию длины
                var results = FindWordsFromLetters(input);

                watch.Stop();

                // Вывод результатов
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

        /// <summary>
        /// Загружает слова из файла и строит для каждого карту частот букв.
        /// </summary>
        private static void InitializeDictionary(string filePath)
        {
            // Читаем все строки из файла UTF-8
            string[] rawWords = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var rawWord in rawWords)
            {
                string word = rawWord.Trim().ToLower();

                // Пропускаем пустые строки или слишком короткие слова
                if (string.IsNullOrEmpty(word)) continue;

                _processedDictionary.Add(new DictionaryWord
                {
                    Word = word,
                    LetterCounts = GetLetterCounts(word)
                });
            }
        }

        /// <summary>
        /// Ищет все слова из словаря, которые можно составить из букв заданного слова.
        /// </summary>
        private static List<string> FindWordsFromLetters(string inputWord)
        {
            int[] inputCounts = GetLetterCounts(inputWord);
            List<string> foundWords = new List<string>();

            foreach (var target in _processedDictionary)
            {
                // Оптимизация: если слово из словаря длиннее исходного, его точно нельзя составить
                if (target.Length > inputWord.Length)
                    continue;

                if (CanFormWord(target.LetterCounts, inputCounts))
                {
                    foundWords.Add(target.Word);
                }
            }

            // Сортировка результатов по уменьшению длины, а затем по алфавиту
            return foundWords
                .OrderByDescending(w => w.Length)
                .ThenBy(w => w)
                .ToList();
        }

        /// <summary>
        /// Проверяет, хватает ли доступных букв (available) для составления целевого слова (target).
        /// </summary>
        private static bool CanFormWord(int[] targetCounts, int[] availableCounts)
        {
            for (int i = 0; i < 33; i++) // 33 буквы русского алфавита
            {
                if (targetCounts[i] > availableCounts[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Вспомогательный метод для подсчета количества каждой русской буквы в слове.
        /// </summary>
        private static int[] GetLetterCounts(string word)
        {
            int[] counts = new int[33]; // Массив под 33 русские буквы
            foreach (char c in word)
            {
                // Поддержка стандартной кириллицы (а-я)
                if (c >= 'а' && c <= 'я')
                {
                    counts[c - 'а']++;
                }
                // Отдельно обрабатываем букву 'ё', если она встречается
                else if (c == 'ё')
                {
                    counts[6]++; // Условно сажаем на одну позицию или обрабатываем как отдельный индекс
                }
            }
            return counts;
        }
    }
}*/