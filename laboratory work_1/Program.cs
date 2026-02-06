using System;

namespace laboratory_work_1
{
    internal class Program
    {
        static long GcdExtended(long a, long b, out long x, out long y)
        {
            if (b == 0)
            {
                x = 1;
                y = 0;
                return a;
            }

            long d = GcdExtended(b, a % b, out long x1, out long y1);

            x = y1;
            y = x1 - y1 * (a / b);

            return d;
        }

        static void Main()
        {
            Console.Write("Введите a: ");
            long a = long.Parse(Console.ReadLine());

            Console.Write("Введите b: ");
            long b = long.Parse(Console.ReadLine());

            long x, y;
            long d = GcdExtended(a, b, out x, out y);

            Console.WriteLine("\nРезультат:");
            Console.WriteLine($"НОД = {d}");
            Console.WriteLine($"x = {x}");
            Console.WriteLine($"y = {y}");
            Console.WriteLine($"\nПроверка: {a}*({x}) + {b}*({y}) = {a * x + b * y}");

            // ==== ДАННЫЕ СТУДЕНТА ====
            Console.WriteLine("\nВыполнил:");
            Console.WriteLine("Коровин Валерий Александрович");
            Console.WriteLine("Группа: 090301-ПОВв-з24");
        }
    }
}
