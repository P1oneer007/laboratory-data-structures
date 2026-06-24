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
        // Оригинальная структура из PDF для хранения профиля каждого абзаца текста
        struct WordProfile
        {
            public string OriginalWord; // Исходный текст абзаца для вывода и подсветки
            public string CleanedWord;  // Полностью очищенный текст в нижнем регистре
            public int[] CharCounts;    // Массив частот букв (размер 58)
            public bool IsFrenchOrEnglish;
        }

        private const int AlphabetSize = 58; // 32 русские (0-31) + 26 латинские (32-57)
        private static List<WordProfile> _dictionaryProfiles = new List<WordProfile>();

        // Модель для хранения точных координат найденных совпадений при интерактивном просмотре
        class SearchMatch
        {
            public int ProfileIndex { get; set; } // Индекс абзаца в базе данных
            public int IndexInText { get; set; }   // Позиция символа внутри этого абзаца
            public int Length { get; set; }        // Длина совпадения
        }

        static void Main(string[] args)
        {
            // Форсируем UTF-8 для консоли
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            string docxPath = "voina-i-mir.docx";

            if (!File.Exists(docxPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка! Файл .docx не найден по пути: {Path.GetFullPath(docxPath)}");
                Console.ResetColor();
                return;
            }

            // 1. ЭТАП ИНИЦИАЛИЗАЦИИ И ПАРСИНГА КОРНЯ DOCX
            Console.WriteLine("Загрузка и индексация словаря из DOCX... Пожалуйста, подождите.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            InitializeFromDocx(docxPath);
            watch.Stop();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Словарь успешно загружен за {watch.ElapsedMilliseconds} мс.");
            Console.WriteLine($"Всего валидных текстовых блоков в базе: {_dictionaryProfiles.Count}");
            Console.ResetColor();
            Console.WriteLine(new string('-', 80));

            // 2. ИНТЕРАКТИВНЫЙ ИНТЕРФЕЙС ПОИСКА И НАВИГАЦИИ
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nВведите слово для поиска (или нажмите Enter для выхода): ");
                Console.ResetColor();

                // Посимвольный ввод для предотвращения сброса буфера Windows при длинных строках
                StringBuilder inputBuilder = new StringBuilder();
                while (true)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        Console.WriteLine();
                        break;
                    }
                    if (keyInfo.Key == ConsoleKey.Backspace && inputBuilder.Length > 0)
                    {
                        inputBuilder.Remove(inputBuilder.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (keyInfo.KeyChar != '\0' && keyInfo.Key != ConsoleKey.Backspace)
                    {
                        inputBuilder.Append(keyInfo.KeyChar);
                        Console.Write(keyInfo.KeyChar);
                    }
                }

                string input = inputBuilder.ToString().Trim();
                if (string.IsNullOrEmpty(input))
                    break;

                Console.Write("Включать французские/латинские слова в поиск? (y/n): ");
                string includeFrenchInput = Console.ReadLine()?.Trim().ToLower();
                bool includeFrench = includeFrenchInput == "y" || includeFrenchInput == "yes";

                // Запуск оригинального алгоритма поиска по 58-символьной матрице
                watch.Restart();
                List<SearchMatch> matches = FindInteractiveMatches(input, includeFrench);
                watch.Stop();

                if (matches.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Из букв этого запроса нельзя составить ни одного фрагмента текста.");
                    Console.ResetColor();
                    continue;
                }

                // Переход в режим PDF/Word ридера для перемещения по результатам
                RunInteractiveViewer(matches, input);
            }
        }

        // Загрузка данных напрямую из OpenXML структуры документа
        static void InitializeFromDocx(string filePath)
        {
            _dictionaryProfiles.Clear();
            using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDocument.MainDocumentPart.Document.Body;
                var paragraphs = body.Descendants<Paragraph>();

                foreach (var para in paragraphs)
                {
                    string rawWord = para.InnerText;
                    if (string.IsNullOrEmpty(rawWord)) continue;
                    rawWord = rawWord.Trim();

                    string cleaned = NormalizeText(rawWord);

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

        // Сканирует базу и находит точные индексы вхождений по вашей анаграммной логике
        static List<SearchMatch> FindInteractiveMatches(string inputWord, bool includeFrench)
        {
            string cleanedInput = NormalizeText(inputWord);
            int[] inputCounts = GetCharCounts(cleanedInput);
            int inputLength = cleanedInput.Length;

            List<SearchMatch> matches = new List<SearchMatch>();

            for (int i = 0; i < _dictionaryProfiles.Count; i++)
            {
                var profile = _dictionaryProfiles[i];

                if (!includeFrench && profile.IsFrenchOrEnglish)
                    continue;

                // Защищенное исправление: Сравнение длин очищенных слов по матрице символов
                int profileCleanedLength = 0;
                for (int j = 0; j < AlphabetSize; j++) profileCleanedLength += profile.CharCounts[j];

                if (profileCleanedLength > inputLength)
                    continue;

                // Если из набора букв можно составить это слово/фрагмент
                if (CanFormWord(profile.CharCounts, inputCounts))
                {
                    // Регистрируем вхождение
                    matches.Add(new SearchMatch
                    {
                        ProfileIndex = i,
                        IndexInText = 0, // Выделяем блок целиком
                        Length = profile.OriginalWord.Length
                    });
                }
            }
            return matches;
        }

        // Интерактивный просмотрщик а-ля PDF/Word с навигацией стрелками
        static void RunInteractiveViewer(List<SearchMatch> matches, string query)
        {
            int currentMatchIndex = 0;

            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"=== Просмотр результатов для набора букв: '{query}' ===");
                Console.WriteLine($"Найдено подходящих блоков: {matches.Count} | Текущий: {currentMatchIndex + 1}");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[Стрелка Вправо] - Вперед | [Стрелка Влево] - Назад | [Escape] - Вернуться к поиску");
                Console.ResetColor();
                Console.WriteLine(new string('-', 80));

                var activeMatch = matches[currentMatchIndex];

                // Выводим контекстное окружение (целевой абзац + соседние для имитации структуры документа)
                int startDocIndex = Math.Max(0, activeMatch.ProfileIndex - 2);
                int endDocIndex = Math.Min(_dictionaryProfiles.Count - 1, activeMatch.ProfileIndex + 2);

                for (int docIdx = startDocIndex; docIdx <= endDocIndex; docIdx++)
                {
                    string text = _dictionaryProfiles[docIdx].OriginalWord;

                    if (docIdx == activeMatch.ProfileIndex)
                    {
                        // Подсвечиваем активный найденный элемент в документе зеленым цветом
                        Console.BackgroundColor = ConsoleColor.DarkGreen;
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($" -> {text} ");
                        Console.ResetColor();
                    }
                    else
                    {
                        // Окружающий текст выводим стандартно
                        Console.WriteLine($"    {text}");
                    }
                }

                Console.WriteLine(new string('-', 80));

                // Считываем нажатия клавиш
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Escape)
                {
                    break;
                }

                else if (key == ConsoleKey.RightArrow)
                {
                    currentMatchIndex = (currentMatchIndex + 1) % matches.Count; // Циклический сдвиг вперед
                }
                else if (key == ConsoleKey.LeftArrow)
                {
                    currentMatchIndex = (currentMatchIndex - 1 + matches.Count) % matches.Count; // Циклический сдвиг назад
                }
            }
        }
        static bool CanFormWord(int wordCounts, int inputCounts)
        {
            for (int i = 0; i < AlphabetSize; i++)
            {
                if (wordCounts(i) > inputCounts(i)) return false;
            }
            return true;
        }
        static int GetCharCounts(string cleanedWord)
        {
            int counts = new int(AlphabetSize);
            foreach (char c in cleanedWord)
            {
                int rusIndex = c - 'а';
                if (rusIndex >= 0 && rusIndex < 32)
                {
                    counts(rusIndex)++;
                    continue;
                }
                int latIndex = c - 'a';
                if (latIndex >= 0 && latIndex < 26)
                {
                    counts(32 + latIndex)++;
                }
            }
            return counts;
        }
        static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
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
    }
}

