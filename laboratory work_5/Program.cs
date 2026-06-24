using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DictionaryWordSearchDocx
{
    class Program
    {
        struct WordProfile
        {
            public string OriginalWord; // Слово в исходном виде для вывода
            public string CleanedWord;  // Очищенное слово в нижнем регистре
            public int[] CharCounts;    // Массив частот букв
            public bool IsFrenchOrEnglish;
        }

        private const int AlphabetSize = 58; // 32 русские + 26 латинские
        private static List<WordProfile> _dictionaryProfiles = new List<WordProfile>();
        private const string LogFilePath = "search_history.txt";

        static void Main(string[] args)
        {
            // Форсируем UTF-8 для консоли
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string docxPath = "voina-i-mir.docx";

            if (!File.Exists(docxPath))
            {
                Console.WriteLine($"Ошибка! Файл .docx не найден по пути: {Path.GetFullPath(docxPath)}");
                return;
            }

            Console.WriteLine("Загрузка и индексация словаря из DOCX... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            InitializeFromDocx(docxPath);

            watch.Stop();
            Console.WriteLine($"Словарь успешно загружен за {watch.ElapsedMilliseconds} мс. Всего слов в базе: {_dictionaryProfiles.Count}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            while (true)
            {
                Console.Write("\nВведите слово (или 'exit' для выхода): ");

                // Читаем поток ввода как чистый UTF-8, чтобы избежать багов терминала VS
                string input = Console.ReadLine();
                if (input != null)
                {
                    byte[] bytes = Encoding.Default.GetBytes(input);
                    input = Encoding.UTF8.GetString(bytes).Trim();
                }

                if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.Write("Включать французские/латинские слова в поиск? (y/n): ");
                string includeFrenchInput = Console.ReadLine()?.Trim().ToLower();
                bool includeFrench = includeFrenchInput == "y" || includeFrenchInput == "yes";

                watch.Restart();
                var result = FindWords(input, includeFrench);
                watch.Stop();

                long elapsedMs = watch.ElapsedMilliseconds;

                Console.WriteLine($"\nНайдено слов: {result.Count} (Время поиска: {elapsedMs} мс)");
                Console.WriteLine("Результаты (по уменьшению длины):");

                int displayLimit = Math.Min(result.Count, 50);
                for (int i = 0; i < displayLimit; i++)
                {
                    Console.WriteLine($"- {result[i]} (длина: {result[i].Length})");
                }
                if (result.Count > 50) Console.WriteLine($"... и еще {result.Count - 50} слов.");

                LogSearchAttempt(input, includeFrench, elapsedMs, result);
            }
        }

        static void InitializeFromDocx(string filePath)
        {
            _dictionaryProfiles.Clear();

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDocument.MainDocumentPart.Document.Body;
                var paragraphs = body.Descendants<Paragraph>();

                foreach (var para in paragraphs)
                {
                    // Извлекаем только чистый текст без скрытых XML-тегов Word
                    string rawWord = para.InnerText;
                    if (string.IsNullOrEmpty(rawWord)) continue;

                    rawWord = rawWord.Trim();

                    // Нормализуем и очищаем от мусора
                    string cleaned = NormalizeText(rawWord);

                    // Если в слове нет ни одной буквы — это разделитель, пропускаем
                    if (string.IsNullOrEmpty(cleaned) || !HasAnyLetter(cleaned))
                        continue;

                    bool isFrenchOrEnglish = HasLatinLetters(cleaned);

                    _dictionaryProfiles.Add(new WordProfile
                    {
                        OriginalWord = rawWord,
                        CleanedWord = cleaned,
                        CharCounts = GetCharCounts(cleaned),
                        IsFrenchOrEnglish = isFrenchOrEnglish
                    });
                }
            }
        }

        static List<string> FindWords(string inputWord, bool includeFrench)
        {
            string cleanedInput = NormalizeText(inputWord);
            int[] inputCounts = GetCharCounts(cleanedInput);
            int inputLength = cleanedInput.Length;

            List<WordProfile> matchedWords = new List<WordProfile>();

            foreach (var profile in _dictionaryProfiles)
            {
                if (!includeFrench && profile.IsFrenchOrEnglish)
                    continue;

                // Сравниваем точную длину очищенных от мусора слов
                if (profile.CleanedWord.Length > inputLength)
                    continue;

                if (CanFormWord(profile.CharCounts, inputCounts))
                {
                    matchedWords.Add(profile);
                }
            }

            // Сортируем по длине очищенного слова
            return matchedWords
                .OrderByDescending(p => p.CleanedWord.Length)
                .ThenBy(p => p.OriginalWord)
                .Select(p => p.OriginalWord)
                .ToList();
        }

        static bool CanFormWord(int[] wordCounts, int[] inputCounts)
        {
            for (int i = 0; i < AlphabetSize; i++)
            {
                if (wordCounts[i] > inputCounts[i])
                    return false;
            }
            return true;
        }

        static int[] GetCharCounts(string cleanedWord)
        {
            int[] counts = new int[AlphabetSize];
            foreach (char c in cleanedWord)
            {
                int rusIndex = c - 'а';
                if (rusIndex >= 0 && rusIndex < 32)
                {
                    counts[rusIndex]++;
                    continue;
                }

                int latIndex = c - 'a';
                if (latIndex >= 0 && latIndex < 26)
                {
                    counts[32 + latIndex]++;
                }
            }
            return counts;
        }

        static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Принудительно переводим строку в нижний регистр инвариантно к культуре ОС
            string result = text.ToLowerInvariant().Replace('ё', 'е');

            StringBuilder sb = new StringBuilder();
            foreach (char c in result)
            {
                switch (c)
                {
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'à': case 'â': case 'æ': sb.Append('a'); break;
                    case 'ô': case 'œ': sb.Append('o'); break;
                    case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'î': case 'ï': sb.Append('i'); break;
                    case 'ç': sb.Append('c'); break;
                    default:
                        // Оставляем строго только маленькие русские и латинские буквы
                        if ((c >= 'а' && c <= 'я') || (c >= 'a' && c <= 'z'))
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        static bool HasAnyLetter(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 'а' && c <= 'я') || (c >= 'a' && c <= 'z')) return true;
            }
            return false;
        }

        static bool HasLatinLetters(string text)
        {
            foreach (char c in text)
            {
                if (c >= 'a' && c <= 'z') return true;
            }
            return false;
        }

        static void LogSearchAttempt(string input, bool includeFrench, long elapsedMs, List<string> results)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                {
                    sw.WriteLine($"=== Попытка поиска: {DateTime.Now:dd.MM.yyyy HH:mm:ss} ===");
                    sw.WriteLine($"Входное слово: {input}");
                    sw.WriteLine($"Включение французского: {(includeFrench ? "Да" : "Нет")}");
                    sw.WriteLine($"Время выполнения: {elapsedMs} мс");
                    sw.WriteLine($"Найдено слов всего: {results.Count}");
                    sw.WriteLine("Список найденных слов (топ-100 по длине):");

                    int logLimit = Math.Min(results.Count, 100);
                    for (int i = 0; i < logLimit; i++)
                    {
                        sw.WriteLine($"{i + 1}. {results[i]} (длина: {results[i].Length})");
                    }
                    if (results.Count > 100) sw.WriteLine($"... и еще {results.Count - 100} слов.");

                    sw.WriteLine(new string('-', 50));
                    sw.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось записать лог в файл: {ex.Message}");
            }
        }
    }
}
/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DictionaryWordSearchDocx
{
    class Program
    {
        struct WordProfile
        {
            public string Word;
            public int[] CharCounts;
            public bool IsFrenchOrEnglish;
        }

        private const int AlphabetSize = 58;
        private static List<WordProfile> _dictionaryProfiles = new List<WordProfile>();
        private const string LogFilePath = "search_history.txt";

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Имя вашего файла .docx в папке с программой
            string docxPath = "voina-i-mir.docx";

            if (!File.Exists(docxPath))
            {
                Console.WriteLine($"Ошибка! Файл .docx не найден по пути: {Path.GetFullPath(docxPath)}");
                return;
            }

            Console.WriteLine("Загрузка и индексация словаря из DOCX... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            InitializeFromDocx(docxPath);

            watch.Stop();
            Console.WriteLine($"Словарь успешно загружен за {watch.ElapsedMilliseconds} мс. Всего слов в базе: {_dictionaryProfiles.Count}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            while (true)
            {
                Console.Write("\nВведите слово (или 'exit' для выхода): ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.Write("Включать французские/латинские слова в поиск? (y/n): ");
                string includeFrenchInput = Console.ReadLine()?.Trim().ToLower();
                bool includeFrench = includeFrenchInput == "y" || includeFrenchInput == "yes";

                watch.Restart();
                var result = FindWords(input, includeFrench);
                watch.Stop();

                long elapsedMs = watch.ElapsedMilliseconds;

                Console.WriteLine($"\nНайдено слов: {result.Count} (Время поиска: {elapsedMs} мс)");
                Console.WriteLine("Результаты (по уменьшению длины):");

                int displayLimit = Math.Min(result.Count, 50);
                for (int i = 0; i < displayLimit; i++)
                {
                    Console.WriteLine($"- {result[i]} (длина: {result[i].Length})");
                }
                if (result.Count > 50) Console.WriteLine($"... и еще {result.Count - 50} слов.");

                LogSearchAttempt(input, includeFrench, elapsedMs, result);
            }
        }

        static void InitializeFromDocx(string filePath)
        {
            _dictionaryProfiles.Clear();

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDocument.MainDocumentPart.Document.Body;
                var paragraphs = body.Descendants<Paragraph>();

                foreach (var para in paragraphs)
                {
                    string rawWord = para.InnerText?.Trim();
                    if (string.IsNullOrEmpty(rawWord)) continue;

                    // Очищаем слово от лишних знаков препинания и пробелов
                    string cleaned = NormalizeText(rawWord);

                    // Если после очистки букв не осталось (были одни тире или цифры) — пропускаем
                    if (string.IsNullOrEmpty(cleaned) || !HasAnyLetter(cleaned))
                        continue;

                    bool isFrenchOrEnglish = HasLatinLetters(cleaned);

                    _dictionaryProfiles.Add(new WordProfile
                    {
                        Word = rawWord,
                        CharCounts = GetCharCounts(cleaned),
                        IsFrenchOrEnglish = isFrenchOrEnglish
                    });
                }
            }
        }
        static List<string> FindWords(string inputWord, bool includeFrench)
        {
            // Очищаем входное слово от мусора, пробелов и переводим в нижний регистр
            string cleanedInput = NormalizeText(inputWord);
            int[] inputCounts = GetCharCounts(cleanedInput);
            int inputLength = cleanedInput.Length;

            List<WordProfile> matchedWords = new List<WordProfile>();

            foreach (var profile in _dictionaryProfiles)
            {
                if (!includeFrench && profile.IsFrenchOrEnglish)
                    continue;

                // ИСПРАВЛЕНИЕ: Считаем длину ОЧИЩЕННОГО слова из словаря
                // Для этого заново быстро считаем сумму всех букв в профиле
                int profileCleanedLength = 0;
                for (int i = 0; i < AlphabetSize; i++)
                {
                    profileCleanedLength += profile.CharCounts[i];
                }

                // Теперь сравниваем только чистые длины букв
                if (profileCleanedLength > inputLength)
                    continue;

                // Проверяем, можно ли составить слово
                if (CanFormWord(profile.CharCounts, inputCounts))
                {
                    matchedWords.Add(profile);
                }
            }

            // Возвращаем результат, сортируя по реальной длине букв
            return matchedWords
                .OrderByDescending(p => {
                    int len = 0;
                    for (int i = 0; i < AlphabetSize; i++) len += p.CharCounts[i];
                    return len;
                })
                .ThenBy(p => p.Word)
                .Select(p => p.Word)
                .ToList();
        }
        static bool CanFormWord(int[] wordCounts, int[] inputCounts)
        {
            for (int i = 0; i < AlphabetSize; i++)
            {
                if (wordCounts[i] > inputCounts[i])
                    return false;
            }
            return true;
        }

        static int[] GetCharCounts(string cleanedWord)
        {
            int[] counts = new int[AlphabetSize];
            foreach (char c in cleanedWord)
            {
                int rusIndex = c - 'а';
                if (rusIndex >= 0 && rusIndex < 32)
                {
                    counts[rusIndex]++;
                    continue;
                }

                int latIndex = c - 'a';
                if (latIndex >= 0 && latIndex < 26)
                {
                    counts[32 + latIndex]++;
                }
            }
            return counts;
        }

        /// <summary>
        /// Переводит в нижний регистр, заменяет ё->е и раскрывает французские акценты.
        /// </summary>
        static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // ВАЖНО: Переводим в нижний регистр ДО любых проверок
            string result = text.ToLower().Replace('ё', 'е');

            StringBuilder sb = new StringBuilder();
            foreach (char c in result)
            {
                switch (c)
                {
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'à': case 'â': case 'æ': sb.Append('a'); break;
                    case 'ô': case 'œ': sb.Append('o'); break;
                    case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'î': case 'ï': sb.Append('i'); break;
                    case 'ç': sb.Append('c'); break;
                    default:
                        // Оставляем только валидные русские или латинские буквы
                        if ((c >= 'а' && c <= 'я') || (c >= 'a' && c <= 'z'))
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        static bool HasAnyLetter(string text)
        {
            foreach (char c in text)
            {
                if ((c >= 'а' && c <= 'я') || (c >= 'a' && c <= 'z')) return true;
            }
            return false;
        }

        static bool HasLatinLetters(string text)
        {
            foreach (char c in text)
            {
                if (c >= 'a' && c <= 'z') return true;
            }
            return false;
        }

        static void LogSearchAttempt(string input, bool includeFrench, long elapsedMs, List<string> results)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                {
                    sw.WriteLine($"=== Попытка поиска: {DateTime.Now:dd.MM.yyyy HH:mm:ss} ===");
                    sw.WriteLine($"Входное слово: {input}");
                    sw.WriteLine($"Включение французского: {(includeFrench ? "Да" : "Нет")}");
                    sw.WriteLine($"Время выполнения: {elapsedMs} мс");
                    sw.WriteLine($"Найдено слов всего: {results.Count}");
                    sw.WriteLine("Список найденных слов (топ-100 по длине):");

                    int logLimit = Math.Min(results.Count, 100);
                    for (int i = 0; i < logLimit; i++)
                    {
                        sw.WriteLine($"{i + 1}. {results[i]} (длина: {results[i].Length})");
                    }
                    if (results.Count > 100) sw.WriteLine($"... и еще {results.Count - 100} слов.");

                    sw.WriteLine(new string('-', 50));
                    sw.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось записать лог в файл: {ex.Message}");
            }
        }
    }
}/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DictionaryWordSearchDocx
{
    class Program
    {
        // Структура для быстрого сопоставления слова и его буквенного профиля
        struct WordProfile
        {
            public string Word;
            public int[] CharCounts; // Частота букв: 0-31 (русские), 32-57 (английские/французские базовые)
            public bool IsFrenchOrEnglish;
        }

        // Размер алфавита: 32 русские (без ё) + 26 латинских
        private const int AlphabetSize = 58;
        private static List<WordProfile> _dictionaryProfiles = new List<WordProfile>();
        private const string LogFilePath = "search_history.txt";

        static void Main(string[] args)
        {
            // Форсируем UTF-8 для консоли, чтобы не было «кракозябр» с кириллицей и французскими символами
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Путь к файлу словаря (измените на свой, если лежит в другом месте)
            string docxPath = "voina-i-mir.docx";

            if (!File.Exists(docxPath))
            {
                Console.WriteLine($"Ошибка! Файл .docx не найден по пути: {Path.GetFullPath(docxPath)}");
                return;
            }

            // 1. ЭТАП ИНИЦИАЛИЗАЦИИ
            Console.WriteLine("Загрузка и индексация словаря из DOCX... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            InitializeFromDocx(docxPath);

            watch.Stop();
            Console.WriteLine($"Словарь успешно загружен за {watch.ElapsedMilliseconds} мс. Всего слов: {_dictionaryProfiles.Count}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // 2. ЭТАП ПОИСКА
            while (true)
            {
                Console.Write("\nВведите слово (или 'exit' для выхода): ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.Write("Включать французские/латинские слова в поиск? (y/n): ");
                string includeFrenchInput = Console.ReadLine()?.Trim().ToLower();
                bool includeFrench = includeFrenchInput == "y" || includeFrenchInput == "yes";

                watch.Restart();

                // Поиск подходящих слов
                var result = FindWords(input, includeFrench);

                watch.Stop();
                long elapsedMs = watch.ElapsedMilliseconds;

                // Вывод результатов в консоль
                Console.WriteLine($"\nНайдено слов: {result.Count} (Время поиска: {elapsedMs} мс)");
                Console.WriteLine("Результаты (по уменьшению длины):");

                int displayLimit = Math.Min(result.Count, 50); // Ограничим вывод в консоль для читаемости
                for (int i = 0; i < displayLimit; i++)
                {
                    Console.WriteLine($"- {result[i]} (длина: {result[i].Length})");
                }
                if (result.Count > 50) Console.WriteLine($"... и еще {result.Count - 50} слов.");

                // Запись попытки в TXT файл
                LogSearchAttempt(input, includeFrench, elapsedMs, result);
            }
        }

        /// <summary>
        /// Читает текст из .docx, фильтрует мусор (тире, линии) и строит частотные профили букв.
        /// </summary>
        static void InitializeFromDocx(string filePath)
        {
            using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDocument.MainDocumentPart.Document.Body;
                var paragraphs = body.Descendants<Paragraph>();

                foreach (var para in paragraphs)
                {
                    string rawWord = para.InnerText?.Trim();
                    if (string.IsNullOrEmpty(rawWord)) continue;

                    // ОПТИМИЗАЦИЯ: Если строка состоит из тире, дефисов или цифр — полностью пропускаем её
                    if (!IsRealWord(rawWord)) continue;

                    // Нормализуем слово (перевод в нижний регистр, замена французских диакритик)
                    string cleaned = NormalizeText(rawWord);
                    bool isFrenchOrEnglish = HasLatinLetters(cleaned);

                    _dictionaryProfiles.Add(new WordProfile
                    {
                        Word = rawWord,
                        CharCounts = GetCharCounts(cleaned),
                        IsFrenchOrEnglish = isFrenchOrEnglish
                    });
                }
            }
        }

        /// <summary>
        /// Проверяет, является ли строка настоящим словом (содержит ли она хотя бы одну букву 
        /// и не состоит ли целиком из спецсимволов/тире).
        /// </summary>
        static bool IsRealWord(string text)
        {
            bool hasLetter = false;
            foreach (char c in text)
            {
                // Проверяем, является ли символ русской или латинской буквой
                if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё' ||
                    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    c == 'é' || c == 'è' || c == 'à' || c == 'ç' || c == 'ù' || c == 'ô' || c == 'î' || c == 'ï')
                {
                    hasLetter = true;
                }

                // Если нашли подозрительные длинные тире, которые часто используются как разделители
                if (c == '—' || c == '–' || c == '_')
                {
                    // Если слово состоит только из них или начинается с них, лучше его пропустить
                    if (!hasLetter) return false;
                }
            }
            return hasLetter;
        }

        /// <summary>
        /// Ищет подходящие слова с учетом настроек фильтрации языка.
        /// </summary>
        static List<string> FindWords(string inputWord, bool includeFrench)
        {
            string cleanedInput = NormalizeText(inputWord);
            int[] inputCounts = GetCharCounts(cleanedInput);
            int inputLength = cleanedInput.Length;

            List<WordProfile> matchedWords = new List<WordProfile>();

            foreach (var profile in _dictionaryProfiles)
            {
                // Если французский отключен, а слово латинское — пропускаем его
                if (!includeFrench && profile.IsFrenchOrEnglish)
                    continue;

                // Быстрое отсечение по длине
                if (profile.Word.Length > inputLength)
                    continue;

                if (CanFormWord(profile.CharCounts, inputCounts))
                {
                    matchedWords.Add(profile);
                }
            }

            // Сортировка по убыванию длины
            return matchedWords
                .OrderByDescending(p => p.Word.Length)
                .Select(p => p.Word)
                .ToList();
        }

        static bool CanFormWord(int[] wordCounts, int[] inputCounts)
        {
            for (int i = 0; i < AlphabetSize; i++)
            {
                if (wordCounts[i] > inputCounts[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Подсчитывает буквы. Индексы 0-31: 'а'-'я'. Индексы 32-57: 'a'-'z'.
        /// </summary>
        static int[] GetCharCounts(string word)
        {
            int[] counts = new int[AlphabetSize];
            foreach (char c in word)
            {
                // Русские буквы
                int rusIndex = c - 'а';
                if (rusIndex >= 0 && rusIndex < 32)
                {
                    counts[rusIndex]++;
                    continue;
                }

                // Английские/Французские базовые буквы
                int latIndex = c - 'a';
                if (latIndex >= 0 && latIndex < 26)
                {
                    counts[32 + latIndex]++;
                }
            }
            return counts;
        }

        /// <summary>
        /// Приводит текст к нижнему регистру, заменяет 'ё' на 'е' и 
        /// раскрывает французские спецсимволы в стандартную латиницу (é -> e, ç -> c и т.д.)
        /// </summary>
        static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string result = text.ToLower().Replace('ё', 'е');

            // Замена французских букв с диакритическими знаками для корректного маппинга букв
            StringBuilder sb = new StringBuilder();
            foreach (char c in result)
            {
                switch (c)
                {
                    case 'é': case 'è': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'à': case 'â': case 'æ': sb.Append('a'); break;
                    case 'ô': case 'œ': sb.Append('o'); break;
                    case 'ù': case 'û': case 'ü': sb.Append('u'); break;
                    case 'î': case 'ï': sb.Append('i'); break;
                    case 'ç': sb.Append('c'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        static bool HasLatinLetters(string text)
        {
            foreach (char c in text)
            {
                if (c >= 'a' && c <= 'z') return true;
            }
            return false;
        }

        /// <summary>
        /// Записывает лог попытки в текстовый файл в кодировке UTF-8.
        /// </summary>
        static void LogSearchAttempt(string input, bool includeFrench, long elapsedMs, List<string> results)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                {
                    sw.WriteLine($"=== Попытка поиска: {DateTime.Now:dd.MM.yyyy HH:mm:ss} ===");
                    sw.WriteLine($"Входное слово: {input}");
                    sw.WriteLine($"Включение французского: {(includeFrench ? "Да" : "Нет")}");
                    sw.WriteLine($"Время выполнения: {elapsedMs} мс");
                    sw.WriteLine($"Найдено слов всего: {results.Count}");
                    sw.WriteLine("Список найденных слов (топ-100 по длине):");

                    int logLimit = Math.Min(results.Count, 100);
                    for (int i = 0; i < logLimit; i++)
                    {
                        sw.WriteLine($"{i + 1}. {results[i]} (длина: {results[i].Length})");
                    }
                    if (results.Count > 100) sw.WriteLine($"... и еще {results.Count - 100} слов.");

                    sw.WriteLine(new string('-', 50));
                    sw.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось записать лог в файл: {ex.Message}");
            }
        }
    }
}
/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DictionaryWordSearch
{
    class Program
    {
        // Структура для быстрого сопоставления слова и его буквенного профиля
        struct WordProfile
        {
            public string Word;
            public int[] CharCounts; // Частота букв от 'а' до 'я' (32 буквы, 'ё' заменяем на 'е')
        }

        private static List<WordProfile> _dictionaryProfiles = new List<WordProfile>();

        static void Main(string[] args)
        {
            // Настройка консоли для корректного вывода UTF-8 в Visual Studio
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Укажите правильный путь к файлу из репозитория Harrix (например, russian_nouns.txt)
            string filePath = "russian_nouns.txt";

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Файл словаря не найден по пути: {Path.GetFullPath(filePath)}");
                Console.WriteLine("Пожалуйста, скачайте файл по ссылке из задания и положите рядом с .exe");
                return;
            }

            // 1. ЭТАП ИНИЦИАЛИЗАЦИИ (выполняется 1 раз)
            Console.WriteLine("Загрузка и индексация словаря... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            InitializeDictionary(filePath);

            watch.Stop();
            Console.WriteLine($"Словарь загружен за {watch.ElapsedMilliseconds} мс. Всего слов: {_dictionaryProfiles.Count}");
            Console.WriteLine("-------------------------------------------------------");

            // 2. ЭТАП ПОИСКА (работает быстрее 2 секунд)
            while (true)
            {
                Console.Write("\nВведите слово (или 'exit' для выхода): ");
                string input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input) || input == "exit")
                    break;

                watch.Restart();

                // Поиск подходящих слов
                var result = FindWords(input);

                watch.Stop();

                // Вывод результатов
                Console.WriteLine($"\nНайдено слов: {result.Count} (Время поиска: {watch.ElapsedMilliseconds} мс)");
                Console.WriteLine("Результаты (по уменьшению длины):");

                foreach (var word in result)
                {
                    Console.WriteLine($"{word} (длина: {word.Length})");
                }
            }
        }

        /// <summary>
        /// Загружает словарь из UTF-8 файла и строит частотные профили букв.
        /// </summary>
        static void InitializeDictionary(string filePath)
        {
            // Читаем все строки в кодировке UTF-8
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (var line in lines)
            {
                string cleaned = line.Trim().ToLower().Replace('ё', 'е');

                if (string.IsNullOrEmpty(cleaned))
                    continue;

                // Оптимизация: пропускаем слова, где есть символы кроме русских букв (дефисы и т.д.)
                if (!IsPureRussian(cleaned))
                    continue;

                _dictionaryProfiles.Add(new WordProfile
                {
                    Word = line.Trim(), // Сохраняем исходный регистр/вид для вывода
                    CharCounts = GetCharCounts(cleaned)
                });
            }
        }

        /// <summary>
        /// Находит все слова, которые можно составить из букв входного слова.
        /// </summary>
        static List<string> FindWords(string inputWord)
        {
            string cleanedInput = inputWord.Replace('ё', 'е');
            int[] inputCounts = GetCharCounts(cleanedInput);
            int inputLength = cleanedInput.Length;

            List<WordProfile> matchedWords = new List<WordProfile>();

            // Ассемблируем подходящие слова линейным проходом (крайне эффективно в C#)
            foreach (var profile in _dictionaryProfiles)
            {
                // Оптимизация: слово из словаря не может быть длиннее входного слова
                if (profile.Word.Length > inputLength)
                    continue;

                if (CanFormWord(profile.CharCounts, inputCounts))
                {
                    matchedWords.Add(profile);
                }
            }

            // Сортировка по уменьшению длины слова
            return matchedWords
                .OrderByDescending(p => p.Word.Length)
                .Select(p => p.Word)
                .ToList();
        }

        /// <summary>
        /// Проверяет, хватает ли букв из inputCounts для составления слова с wordCounts.
        /// </summary>
        static bool CanFormWord(int[] wordCounts, int[] inputCounts)
        {
            for (int i = 0; i < 32; i++)
            {
                if (wordCounts[i] > inputCounts[i])
                    return false; // Букв не хватает
            }
            return true;
        }

        /// <summary>
        /// Строит частотный массив для русского алфавита ('а' = индекс 0, 'я' = индекс 31).
        /// </summary>
        static int[] GetCharCounts(string word)
        {
            int[] counts = new int[32];
            foreach (char c in word)
            {
                int index = c - 'а';
                if (index >= 0 && index < 32)
                {
                    counts[index]++;
                }
            }
            return counts;
        }

        /// <summary>
        /// Проверка, состоит ли слово только из строчных русских букв.
        /// </summary>
        static bool IsPureRussian(string word)
        {
            foreach (char c in word)
            {
                if (c < 'а' || c > 'я')
                    return false;
            }
            return true;
        }
    }
}/*using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        // Настройка кодировки для корректного отображения кириллицы
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        try { Console.BufferHeight = 5000; } catch { }

        // Укажите путь к вашему исходному файлу .docx
        string dictPath = "D:\\data\\downloads\\voina-i-mir.docx";

        if (!File.Exists(dictPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Ошибка] Файл словаря '{dictPath}' не найден в папке с программой!");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Инициализация системы ===");
        Console.ResetColor();
        Console.WriteLine("Чтение и обработка словаря из .docx файла...");

        List<string> dictionary = LoadDictionaryFromDocx(dictPath);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Словарь успешно загружен! Найдено уникальных русских слов: {dictionary.Count:N0}\n");
        Console.ResetColor();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Введите исходное слово (или 'exit' для выхода): ");
            Console.ResetColor();

            string sourceWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(sourceWord)) continue;
            if (sourceWord == "exit") break;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            List<string> foundWords = FindWords(sourceWord, dictionary);
            watch.Stop();

            Console.WriteLine();
            Console.WriteLine($"Найдено слов всего: {foundWords.Count} (Поиск занял: {watch.ElapsedMilliseconds} мс)");
            Console.WriteLine("--------------------------------------------------");

            if (foundWords.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Совпадений не найдено.");
                Console.ResetColor();
                continue;
            }

            int pageSize = 20;
            int totalWords = foundWords.Count;

            for (int i = 0; i < totalWords; i += pageSize)
            {
                var page = foundWords.Skip(i).Take(pageSize);

                foreach (var word in page)
                {
                    string alignedWord = word.PadRight(25);
                    Console.WriteLine($"  - {alignedWord} [{word.Length} букв]");
                }

                if (i + pageSize < totalWords)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\nПоказано {i + page.Count()} из {totalWords}. Нажмите ENTER для продолжения просмотра...");
                    Console.ResetColor();
                    Console.ReadLine();
                    Console.SetCursorPosition(0, Console.CursorTop - 2);
                }
            }
            Console.WriteLine("--------------------------------------------------");
        }
    }

    /// <summary>
    /// Извлекает текст напрямую из структуры zip-архива .docx без сторонних библиотек
    /// </summary>
    static List<string> LoadDictionaryFromDocx(string path)
    {
        var uniqueWords = new HashSet<string>();

        try
        {
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                // Главный текстовый контент в docx всегда лежит в этом файле внутри архива
                ZipArchiveEntry entry = archive.GetEntry("word/document.xml");
                if (entry != null)
                {
                    using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    {
                        string xmlContent = reader.ReadToEnd();

                        // Регулярное выражение находит только последовательности русских букв (включая ё и дефис)
                        // Это полностью отсекает XML-теги и бинарный мусор
                        var matches = Regex.Matches(xmlContent, @"[а-яА-ЯёЁ-]+");

                        foreach (Match match in matches)
                        {
                            string cleanWord = match.Value.Trim().ToLower();

                            // Исключаем одиночные буквы-мусор и пустые строки
                            if (cleanWord.Length > 1)
                            {
                                uniqueWords.Add(cleanWord);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Ошибка чтения .docx]: {ex.Message}");
            Console.ResetColor();
        }

        return uniqueWords.ToList();
    }

    static List<string> FindWords(string sourceWord, List<string> dictionary)
    {
        List<string> result = new List<string>();
        int[] sourceLetterCounts = GetLetterCounts(sourceWord);

        foreach (var word in dictionary)
        {
            if (word.Length > sourceWord.Length) continue;

            if (CanFormWord(word, sourceLetterCounts))
            {
                result.Add(word);
            }
        }

        return result
            .OrderByDescending(w => w.Length)
            .ThenBy(w => w)
            .ToList();
    }

    static int[] GetLetterCounts(string word)
    {
        int[] counts = new int[34];
        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            counts[index]++;
        }
        return counts;
    }

    static bool CanFormWord(string word, int[] sourceCounts)
    {
        Span<int> currentCounts = stackalloc int[34];

        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            currentCounts[index]++;

            if (currentCounts[index] > sourceCounts[index])
            {
                return false;
            }
        }
        return true;
    }

    static int GetLetterIndex(char c)
    {
        if (c == 'ё') return 32;
        int idx = c - 'а';
        if (idx >= 0 && idx < 32) return idx;
        return 33;
    }
}

/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        // Настройка кодировки для корректного отображения кириллицы
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Попытка увеличить буфер консоли (чтобы можно было прокручивать назад)
        try { Console.BufferHeight = 5000; } catch { }

        // !!! ВАЖНО: Файл должен быть предварительно пересохранен из .docx в .txt (в UTF-8)
        string dictPath = "D:\\data\\downloads\\voina-i-mir.docx";

        if (!File.Exists(dictPath))
        {
            Console.WriteLine($"[Ошибка] Файл словаря '{dictPath}' не найден!");
            Console.WriteLine("Пожалуйста, пересохраните ваш .docx в формате .txt (Юникод UTF-8).");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Инициализация системы ===");
        Console.ResetColor();
        Console.WriteLine("Чтение и обработка словаря...");

        List<string> dictionary = LoadDictionary(dictPath);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Словарь успешно загружен! Найдено уникальных слов: {dictionary.Count:N0}\n");
        Console.ResetColor();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Введите исходное слово (или 'exit' для выхода): ");
            Console.ResetColor();

            string sourceWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(sourceWord)) continue;
            if (sourceWord == "exit") break;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            List<string> foundWords = FindWords(sourceWord, dictionary);
            watch.Stop();

            Console.WriteLine();
            Console.WriteLine($"Найдено слов всего: {foundWords.Count} (Поиск занял: {watch.ElapsedMilliseconds} мс)");
            Console.WriteLine("--------------------------------------------------");

            if (foundWords.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Совпадений не найдено.");
                Console.ResetColor();
                continue;
            }

            // Постраничный вывод результатов
            int pageSize = 20; // Сколько слов выводить за один раз
            int totalWords = foundWords.Count;

            for (int i = 0; i < totalWords; i += pageSize)
            {
                // Берем очередную порцию слов
                var page = foundWords.Skip(i).Take(pageSize);

                foreach (var word in page)
                {
                    // Форматируем вывод: выравниваем слово по левому краю в пределах 25 символов
                    string alignedWord = word.PadRight(25);
                    Console.WriteLine($"  - {alignedWord} [{word.Length} букв]");
                }

                // Если это не последняя страница, ждем нажатия клавиши
                if (i + pageSize < totalWords)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\nПоказано {i + page.Count()} из {totalWords}. Нажмите ENTER для продолжения просмотра...");
                    Console.ResetColor();
                    Console.ReadLine();
                    // Стираем строку ожидания, чтобы вывод шел сплошным текстом
                    Console.SetCursorPosition(0, Console.CursorTop - 2);
                }
            }
            Console.WriteLine("--------------------------------------------------");
        }
    }

    static List<string> LoadDictionary(string path)
    {
        var uniqueWords = new HashSet<string>();
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            string cleanWord = line.Trim().ToLower();
            // Дополнительная очистка от мусора (пропускаем слова с цифрами или спецсимволами)
            if (!string.IsNullOrEmpty(cleanWord) && cleanWord.All(c => char.IsLetter(c) || c == '-'))
            {
                uniqueWords.Add(cleanWord);
            }
        }
        return uniqueWords.ToList();
    }

    static List<string> FindWords(string sourceWord, List<string> dictionary)
    {
        List<string> result = new List<string>();
        int[] sourceLetterCounts = GetLetterCounts(sourceWord);

        foreach (var word in dictionary)
        {
            if (word.Length > sourceWord.Length) continue;

            if (CanFormWord(word, sourceLetterCounts))
            {
                result.Add(word);
            }
        }

        return result
            .OrderByDescending(w => w.Length)
            .ThenBy(w => w)
            .ToList();
    }

    static int[] GetLetterCounts(string word)
    {
        int[] counts = new int[34];
        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            counts[index]++;
        }
        return counts;
    }

    static bool CanFormWord(string word, int[] sourceCounts)
    {
        Span<int> currentCounts = stackalloc int[34];

        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            currentCounts[index]++;

            if (currentCounts[index] > sourceCounts[index])
            {
                return false;
            }
        }
        return true;
    }

    static int GetLetterIndex(char c)
    {
        if (c == 'ё') return 32;
        int idx = c - 'а';
        if (idx >= 0 && idx < 32) return idx;
        return 33;
    }
}

/*using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        // Принудительно устанавливаем кодировку UTF-8 для консоли
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // Укажите путь к вашему txt-файлу со словарем (например, скачанному с GitHub)
        string dictPath = "D:\\data\\downloads\\voina-i-mir.docx";

        // Создаем демо-файл, если словаря еще нет на диске
        if (!File.Exists(dictPath))
        {
            File.WriteAllLines(dictPath, new[] { "карта", "граф", "графство", "радио", "кардиограф", "тигр", "актер", "рентген" }, Encoding.UTF8);
            Console.WriteLine($"[Инфо] Создан демонстрационный словарь: {dictPath}");
        }

        // Этап инициализации (выполняется 1 раз)
        Console.WriteLine("Чтение и обработка словаря...");
        List<string> dictionary = LoadDictionary(dictPath);
        Console.WriteLine($"Словарь успешно загружен! Найдено уникальных русских слов: {dictionary.Count}");

        while (true)
        {
            Console.Write("\nВведите исходное слово (или 'exit' для выхода): ");
            string sourceWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(sourceWord)) continue;
            if (sourceWord == "exit") break;

            // Засекаем время поиска для проверки лимита в 2 секунды
            var watch = System.Diagnostics.Stopwatch.StartNew();

            List<string> foundWords = FindWords(sourceWord, dictionary);

            watch.Stop();

            Console.WriteLine($"\nНайденные слова (всего {foundWords.Count}), поиск занял {watch.ElapsedMilliseconds} мс:");
            foreach (var word in foundWords)
            {
                Console.WriteLine($"- {word} ({word.Length} букв)");
            }
        }
    }

    /// <summary>
    /// Загружает и очищает уникальные слова из файла.
    /// </summary>
    static List<string> LoadDictionary(string path)
    {
        var uniqueWords = new HashSet<string>();

        // Читаем файл построчно в UTF-8
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            string cleanWord = line.Trim().ToLower();
            if (!string.IsNullOrEmpty(cleanWord))
            {
                uniqueWords.Add(cleanWord);
            }
        }
        return uniqueWords.ToList();
    }

    /// <summary>
    /// Ищет слова из словаря, которые можно составить из букв исходного слова.
    /// </summary>
    static List<string> FindWords(string sourceWord, List<string> dictionary)
    {
        List<string> result = new List<string>();

        // Частотная карта для исходного слова
        int[] sourceLetterCounts = GetLetterCounts(sourceWord);

        foreach (var word in dictionary)
        {
            // Оптимизация 1: Слово из словаря не может быть длиннее исходного
            if (word.Length > sourceWord.Length) continue;

            // Оптимизация 2: Быстрая посимвольная проверка частот
            if (CanFormWord(word, sourceLetterCounts))
            {
                result.Add(word);
            }
        }

        // Сортировка: сначала по убыванию длины, затем по алфавиту
        return result
            .OrderByDescending(w => w.Length)
            .ThenBy(w => w)
            .ToList();
    }

    /// <summary>
    /// Считает количество вхождений каждой русской буквы ('а'..'я' и 'ё').
    /// </summary>
    static int[] GetLetterCounts(string word)
    {
        // 34 ячейки: 33 для алфавита и 1 для некорректных символов (если будут)
        int[] counts = new int[34];
        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            counts[index]++;
        }
        return counts;
    }

    /// <summary>
    /// Проверяет, хватает ли букв из карты sourceCounts для составления слова.
    /// </summary>
    static bool CanFormWord(string word, int[] sourceCounts)
    {
        // Создаем локальную копию счетчиков для текущего слова, чтобы не аллоцировать память,
        // используем стек через Span для максимальной скорости (доступно в .NET Core / .NET 5+)
        Span<int> currentCounts = stackalloc int[34];

        foreach (char c in word)
        {
            int index = GetLetterIndex(c);
            currentCounts[index]++;

            // Если в слове из словаря этой буквы нужно больше, чем есть у пользователя — сразу выходим
            if (currentCounts[index] > sourceCounts[index])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Превращает кириллическую букву в индекс массива от 0 до 32.
    /// </summary>
    static int GetLetterIndex(char c)
    {
        if (c == 'ё') return 32;
        int idx = c - 'а';
        if (idx >= 0 && idx < 32) return idx;
        return 33; // Для любых недопустимых символов (дефисы, пробелы)
    }
}

/*using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    class WordData
    {
        public string Word { get; set; } = string.Empty;
        public Dictionary<char, int> LetterCounts { get; set; } = new();
    }

    static Dictionary<char, int> GetLetterCounts(string str)
    {
        var counts = new Dictionary<char, int>();
        foreach (char ch in str)
        {
            if (counts.ContainsKey(ch)) counts[ch]++;
            else counts[ch] = 1;
        }
        return counts;
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string docxPath = "D:\\data\\downloads\\voina-i-mir.docx";

        if (!File.Exists(docxPath))
        {
            Console.WriteLine($"Ошибка: Файл {docxPath} не найден.");
            return;
        }

        Console.WriteLine("Чтение и обработка словаря из .docx файла...");
        var dictionary = new List<WordData>();

        try
        {
            StringBuilder fullTextBuilder = new StringBuilder();

            using (ZipArchive archive = ZipFile.OpenRead(docxPath))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null)
                {
                    Console.WriteLine("Ошибка: Неверная структура .docx файла.");
                    return;
                }

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string xmlContent = reader.ReadToEnd();

                    var xmlMatches = Regex.Matches(xmlContent, @"<w:t[^>]*>(.*?)</w:t>");
                    foreach (Match m in xmlMatches)
                    {
                        fullTextBuilder.Append(m.Groups[1].Value); // Исправлено: берем чистую группу внутри тегов
                    }
                }
            }

            string fullText = fullTextBuilder.ToString();

            // КРИТИЧЕСКИЙ ШАГ: Удаляем скрытые спецсимволы Word (мягкие переносы, неразрывные пробелы и т.д.)
            fullText = fullText.Replace("\u00ad", ""); // Удаляем мягкий перенос (Soft Hyphen)
            fullText = fullText.Replace("\u00a0", " "); // Заменяем неразрывный пробел на обычный
            fullText = fullText.Replace("\u200b", ""); // Удаляем нулевой пробел (Zero-width space)

            // Заменяем французские буквы, цифры и знаки препинания на пробелы
            fullText = Regex.Replace(fullText, @"[a-zA-ZàâäéèêëîïôöùûüçÀÂÄÉÈÊËÎÏÔÖÙÛÜÇ0-9«»""'“”‘’\-\.,;:!\?_\(\)\[\]\{\}\/\\]", " ");

            // Выделяем только русские слова
            var wordMatches = Regex.Matches(fullText.ToLower(), @"\b[а-яё]+\b");

            HashSet<string> uniqueWords = new HashSet<string>();

            foreach (Match match in wordMatches)
            {
                string word = match.Value.Trim();
                if (word.Length >= 1)
                {
                    uniqueWords.Add(word);
                }
            }

            foreach (string word in uniqueWords)
            {
                dictionary.Add(new WordData
                {
                    Word = word,
                    LetterCounts = GetLetterCounts(word)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка при обработке файла: {ex.Message}");
            return;
        }

        Console.WriteLine($"Словарь успешно загружен! Найдено уникальных русских слов: {dictionary.Count}");

        // ОСНОВНОЙ ЦИКЛ ПОИСКА
        while (true)
        {
            Console.Write("\nВведите исходное слово (или 'exit' для выхода): ");
            string? targetWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(targetWord) || targetWord == "exit")
                break;

            var targetCounts = GetLetterCounts(targetWord);
            var resultWords = new List<string>();

            foreach (var wd in dictionary)
            {
                // Условие задачи: найти слова, которые можно составить ИЗ букв данного слова.
                // Значит, длина слова из словаря должна быть МЕНЬШЕ или РАВНА длине введенного слова.
                if (wd.Word.Length > targetWord.Length) continue;

                bool canForm = true;
                foreach (var pair in wd.LetterCounts)
                {
                    char letter = pair.Key;
                    int countNeeded = pair.Value;

                    if (!targetCounts.TryGetValue(letter, out int countAvailable) || countAvailable < countNeeded)
                    {
                        canForm = false;
                        break;
                    }
                }

                if (canForm)
                {
                    resultWords.Add(wd.Word);
                }
            }

            var sortedResults = resultWords.OrderByDescending(w => w.Length).ToList();

            Console.WriteLine($"\nНайденные слова (всего {sortedResults.Count}):");
            foreach (var word in sortedResults)
            {
                Console.WriteLine(word);
            }
        }
    }
}/*using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    class WordData
    {
        public string Word { get; set; } = string.Empty;
        public Dictionary<char, int> LetterCounts { get; set; } = new();
    }

    static Dictionary<char, int> GetLetterCounts(string str)
    {
        var counts = new Dictionary<char, int>();
        foreach (char ch in str)
        {
            if (counts.ContainsKey(ch)) counts[ch]++;
            else counts[ch] = 1;
        }
        return counts;
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string docxPath = "D:\\data\\downloads\\voina-i-mir.docx"; // Укажите имя вашего файла

        if (!File.Exists(docxPath))
        {
            Console.WriteLine($"Ошибка: Файл {docxPath} не найден.");
            return;
        }

        Console.WriteLine("Чтение и обработка словаря из .docx файла...");
        var dictionary = new List<WordData>();

        try
        {
            StringBuilder fullTextBuilder = new StringBuilder();

            // 1. Извлекаем ВЕСЬ текст из документа в одну строку
            using (ZipArchive archive = ZipFile.OpenRead(docxPath))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null)
                {
                    Console.WriteLine("Ошибка: Неверная структура .docx файла.");
                    return;
                }

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string xmlContent = reader.ReadToEnd();

                    // Находим абсолютно все кусочки текста
                    var xmlMatches = Regex.Matches(xmlContent, @"<w:t[^>]*>(.*?)</w:t>");
                    foreach (Match m in xmlMatches)
                    {
                        fullTextBuilder.Append(m.Groups[1].Value);
                    }
                }
            }

            string fullText = fullTextBuilder.ToString();

            // 2. Очищаем текст: удаляем французские слова и сноски.
            // Исключаем латиницу и символы с французской диакритикой (é, è, à, ç, ù, â, ê, î, ô, û, ë, ï, ü)
            // Заменяем их на пробелы, чтобы они не склеивали русские слова
            fullText = Regex.Replace(fullText, @"[a-zA-ZàâäéèêëîïôöùûüçÀÂÄÉÈÊËÎÏÔÖÙÛÜÇ0-9«»""'“”‘’\-\.,;:!\?]", " ");

            // 3. Выделяем только чистые русские слова
            // [а-яё]+ и граница слова \b гарантируют, что мы берем целые слова
            var wordMatches = Regex.Matches(fullText.ToLower(), @"\b[а-яё]+\b");

            // Хеш-сет, чтобы не добавлять дубликаты и не тратить память
            HashSet<string> uniqueWords = new HashSet<string>();

            foreach (Match match in wordMatches)
            {
                string word = match.Value;
                if (word.Length > 1) // Игнорируем одиночные буквы (если нужно искать и по 1 букве, удалите это условие)
                {
                    uniqueWords.Add(word);
                }
            }

            // 4. Заполняем структуру данных для поиска
            foreach (string word in uniqueWords)
            {
                dictionary.Add(new WordData
                {
                    Word = word,
                    LetterCounts = GetLetterCounts(word)
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка при обработке файла: {ex.Message}");
            return;
        }

        Console.WriteLine($"Словарь успешно загружен! Найдено уникальных русских слов: {dictionary.Count}");

        // ОСНОВНОЙ ЦИКЛ ПОИСКА
        while (true)
        {
            Console.Write("\nВведите исходное слово (или 'exit' для выхода): ");
            string? targetWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(targetWord) || targetWord == "exit")
                break;

            var targetCounts = GetLetterCounts(targetWord);
            var resultWords = new List<string>();

            foreach (var wd in dictionary)
            {
                if (wd.Word.Length > targetWord.Length) continue;

                bool canForm = true;
                foreach (var pair in wd.LetterCounts)
                {
                    char letter = pair.Key;
                    int countNeeded = pair.Value;

                    if (!targetCounts.TryGetValue(letter, out int countAvailable) || countAvailable < countNeeded)
                    {
                        canForm = false;
                        break;
                    }
                }

                if (canForm)
                {
                    resultWords.Add(wd.Word);
                }
            }

            var sortedResults = resultWords.OrderByDescending(w => w.Length).ToList();

            Console.WriteLine($"\nНайденные слова (всего {sortedResults.Count}):");
            foreach (var word in sortedResults)
            {
                Console.WriteLine(word);
            }
        }
    }
}

/*using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

class Program
{
    class WordData
    {
        public string Word { get; set; } = string.Empty;
        public Dictionary<char, int> LetterCounts { get; set; } = new();
    }

    static Dictionary<char, int> GetLetterCounts(string str)
    {
        var counts = new Dictionary<char, int>();
        foreach (char ch in str)
        {
            if (counts.ContainsKey(ch)) counts[ch]++;
            else counts[ch] = 1;
        }
        return counts;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        string docxPath = "D:\\data\\downloads\\voina-i-mir.docx"; // Путь к вашему документу Word

        if (!File.Exists(docxPath))
        {
            Console.WriteLine($"Ошибка: Файл {docxPath} не найден.");
            return;
        }

        Console.WriteLine("Чтение и обработка словаря из .docx файла...");
        var dictionary = new List<WordData>();

        try
        {
            // ЭТАП ИНИЦИАЛИЗАЦИИ: Открываем docx как ZIP-архив средствами .NET
            using (ZipArchive archive = ZipFile.OpenRead(docxPath))
            {
                // Главный текст в Word хранится в файле word/document.xml
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null)
                {
                    Console.WriteLine("Ошибка: Неверная структура .docx файла.");
                    return;
                }

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string xmlContent = reader.ReadToEnd();

                    // Очищаем XML-теги, вытаскивая только текстовое содержимое
                    // Тег <w:t> в Word обозначает текстовые узлы
                    var matches = Regex.Matches(xmlContent, @"<w:t[^>]*>(.*?)</w:t>");

                    foreach (Match match in matches)
                    {
                        string word = match.Groups[1].Value.Trim().ToLower(); // приводим к нижнему регистру для надежности

                        // Проверяем, что это именно слово, а не пробелы или знаки препинания
                        if (string.IsNullOrEmpty(word) || !Regex.IsMatch(word, @"^[а-яёa-z]+$"))
                            continue;

                        dictionary.Add(new WordData
                        {
                            Word = word,
                            LetterCounts = GetLetterCounts(word)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка при чтении файла: {ex.Message}");
            return;
        }

        Console.WriteLine($"Словарь успешно загружен! Найдено слов: {dictionary.Count}");

        // ОСНОВНОЙ ЦИКЛ ПОИСКА (Выполняется мгновенно, значительно быстрее 2 секунд)
        while (true)
        {
            Console.Write("\nВведите исходное слово (или 'exit' для выхода): ");
            string? targetWord = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(targetWord) || targetWord == "exit")
                break;

            var targetCounts = GetLetterCounts(targetWord);
            var resultWords = new List<string>();

            foreach (var wd in dictionary)
            {
                if (wd.Word.Length > targetWord.Length) continue;

                bool canForm = true;
                foreach (var pair in wd.LetterCounts)
                {
                    char letter = pair.Key;
                    int countNeeded = pair.Value;

                    if (!targetCounts.TryGetValue(letter, out int countAvailable) || countAvailable < countNeeded)
                    {
                        canForm = false;
                        break;
                    }
                }

                if (canForm)
                {
                    resultWords.Add(wd.Word);
                }
            }

            // Сортировка результатов по убыванию длины слова
            var sortedResults = resultWords.Distinct().OrderByDescending(w => w.Length).ToList();

            Console.WriteLine($"\nНайденные слова (всего {sortedResults.Count}):");
            foreach (var word in sortedResults)
            {
                Console.WriteLine(word);
            }
        }
    }
}*/