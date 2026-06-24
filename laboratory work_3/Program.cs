using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace laboratory_work_3
{
    internal class Program
    {
        struct Point
        {
            public int X, Y;
            public Point(int x, int y) { X = x; Y = y; }
        }

        static void Main()
        {
            int N = 30000;
            string[] moves = GenerateMoves(N);

            Console.WriteLine($"Количество ходов: {N}\n");

            TestArray(moves);
            TestLinkedList(moves);
            TestHashSet(moves);

            Console.WriteLine("\nВыполнил:");
            Console.WriteLine("Коровин Валерий Александрович");
            Console.WriteLine("Группа: 090301-ПОВв-з24");
        }

        // Array. Тестирование через Динамический массив 
        static void TestArray(string[] moves)
        {
            var sw = Stopwatch.StartNew();

            List<Point> visited = new List<Point>();
            int x = 0, y = 0;
            visited.Add(new Point(0, 0));

            bool repeat = false;

            foreach (var m in moves)
            {
                Move(ref x, ref y, m);

                foreach (var p in visited)
                    if (p.X == x && p.Y == y)
                    {
                        repeat = true;
                        goto end;
                    }

                visited.Add(new Point(x, y));
            }

        end:
            sw.Stop();
            Console.WriteLine($"Array: repeat={repeat}, time={sw.Elapsed.TotalMilliseconds} ms");
        }

        // Linked List. Тестирование через Связанный список
        static void TestLinkedList(string[] moves)
        {
            var sw = Stopwatch.StartNew();

            LinkedList<Point> visited = new LinkedList<Point>();
            int x = 0, y = 0;
            visited.AddLast(new Point(0, 0));

            bool repeat = false;

            foreach (var m in moves)
            {
                Move(ref x, ref y, m);

                foreach (var p in visited)
                    if (p.X == x && p.Y == y)
                    {
                        repeat = true;
                        goto end;
                    }

                visited.AddLast(new Point(x, y));
            }

        end:
            sw.Stop();
            Console.WriteLine($"LinkedList: repeat={repeat}, time={sw.Elapsed.TotalMilliseconds} ms");
        }

        // Hashset. Тестирование через Хэш-таблицу
        static void TestHashSet(string[] moves)
        {
            var sw = Stopwatch.StartNew();

            HashSet<(int, int)> visited = new HashSet<(int, int)>();
            int x = 0, y = 0;
            visited.Add((0, 0));

            bool repeat = false;

            foreach (var m in moves)
            {
                Move(ref x, ref y, m);

                if (!visited.Add((x, y)))
                {
                    repeat = true;
                    break;
                }
            }

            sw.Stop();
            Console.WriteLine($"HashSet: repeat={repeat}, time={sw.Elapsed.TotalMilliseconds} ms");
        }

        // Move. Логика движения короля
        static void Move(ref int x, ref int y, string m)
        {
            switch (m)
            {
                case "U": y++; break;
                case "D": y--; break;
                case "L": x--; break;
                case "R": x++; break;
                case "UL": x--; y++; break;
                case "UR": x++; y++; break;
                case "DL": x--; y--; break;
                case "DR": x++; y--; break;
            }
        }

        // Generator. генерация ходов
        static string[] GenerateMoves(int n)
        {
            string[] dirs = { /*"U", "D",*/ "L", "R" /*, "UL", "UR", "DL", "DR"*/ }; 
            Random r = new Random();
            string[] m = new string[n];

            for (int i = 0; i < n; i++)
                m[i] = dirs[r.Next(dirs.Length)];

            return m;
        }
    }
}