using System;
using System.Diagnostics;
using System.Numerics; // для Vector<float>
using System.Threading.Tasks; // Добавлено для Parallel
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;

namespace laboratory_work_2
{
    internal class Program
    {
        const int N = 4096;

        static void Main()
        {
            Console.WriteLine($"Размер матриц: {N}x{N}\n");

            // Для классического и блочного используем одномерные массивы (строка за строкой)
            // Это критически важно для скорости работы в .NET
            float[] A = new float[N * N];
            float[] B = new float[N * N];
            float[] C = new float[N * N];

            Random rnd = new Random();
            for (int i = 0; i < N * N; i++)
            {
                A[i] = (float)rnd.NextDouble();
                B[i] = (float)rnd.NextDouble();
            }

            double c = 2.0 * N * N * N;

            // 1. Обычный алгоритм
            var sw = Stopwatch.StartNew();
            // MultiplyClassic(A, B, C, N); 
            Console.WriteLine("Пропуск ...");
            sw.Stop();
            PrintStats("Classic", sw.Elapsed.TotalSeconds, c);

            // 2. BLAS (MathNet)
            // Конвертируем одномерные массивы для MathNet
            var mA = new DenseMatrix(N, N, A);
            var mB = new DenseMatrix(N, N, B);
            sw.Restart();
            var mC = mA * mB;
            sw.Stop();
            PrintStats("BLAS (MathNet)", sw.Elapsed.TotalSeconds, c);

            // 3. Оптимизированный алгоритм (Транспонирование + Parallel + Векторные инструкции (SIMD))
            Array.Clear(C, 0, C.Length); // Обнуляем результирующую матрицу
            sw.Restart();
            MultiplyOptimizedParallel(A, B, C, N);
            sw.Stop();
            PrintStats("Optimized (Parallel + Transpose + Vector instructions (SIMD))", sw.Elapsed.TotalSeconds, c);

            Console.WriteLine("\nВыполнил:");
            Console.WriteLine("Коровин Валерий Александрович");
            Console.WriteLine("Группа: 090301-ПОВв-з24");
        }

        // Классическое перемножение (адаптировано под одномерный массив)
        static void MultiplyClassic(float[] A, float[] B, float[] C, int n)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += A[i * n + k] * B[k * n + j];
                    }
                    C[i * n + j] = sum;
                }
            }
        }

        static void MultiplyOptimizedParallel(float[] A, float[] B, float[] C, int n)
        {
            float[] BT = new float[n * n];

            // 1. Быстрое параллельное транспонирование
            Parallel.For(0, n, i =>
            {
                for (int j = 0; j < n; j++)
                {
                    BT[j * n + i] = B[i * n + j];
                }
            });

            // Узнаем, сколько элементов float помещается в один вектор процессора
            int vectorSize = System.Numerics.Vector<float>.Count;

            // 2. Параллельное вычисление с SIMD-векторизацией
            Parallel.For(0, n, i =>
            {
                int rowAOffset = i * n;
                for (int j = 0; j < n; j++)
                {
                    int rowBTOffset = j * n;

                    // Вектор для накопления частичных сумм
                    System.Numerics.Vector<float> sumVector = System.Numerics.Vector<float>.Zero;
                    int k = 0;

                    // Шаг векторного умножения (обрабатываем по vectorSize элементов за раз)
                    for (; k <= n - vectorSize; k += vectorSize)
                    {
                        var va = new System.Numerics.Vector<float>(A, rowAOffset + k);
                        var vb = new System.Numerics.Vector<float>(BT, rowBTOffset + k);
                        sumVector += va * vb; // Аппаратное умножение и сложение нескольких float сразу
                    }

                    // Сворачиваем вектор в одну скалярную сумму
                    float finalSum = System.Numerics.Vector.Dot(sumVector, System.Numerics.Vector<float>.One);

                    // Досчитываем оставшиеся «хвосты», если размер матрицы не кратен размеру вектора
                    for (; k < n; k++)
                    {
                        finalSum += A[rowAOffset + k] * BT[rowBTOffset + k];
                    }

                    C[rowAOffset + j] = finalSum;
                }
            });
        }

      /* static void MultiplyOptimizedParallel(float[] A, float[] B, float[] C, int n)
        {
            float[] BT = new float[n * n];

            // Распараллеленное транспонирование матрицы B
            Parallel.For(0, n, i =>
            {
                for (int j = 0; j < n; j++)
                {
                    BT[j * n + i] = B[i * n + j];
                }
            });

            // Параллельное вычисление строк матрицы C
            Parallel.For(0, n, i =>
            {
                int rowAOffset = i * n;
                for (int j = 0; j < n; j++)
                {
                    int rowBTOffset = j * n;
                    float sum = 0;

                    for (int k = 0; k < n; k++)
                    {
                        sum += A[rowAOffset + k] * BT[rowBTOffset + k];
                    }
                    C[rowAOffset + j] = sum;
                }
            });
        }*/

        static void PrintStats(string name, double t, double c)
        {
            double p = c / t * 1e-6;
            Console.WriteLine($"{name}:");
            Console.WriteLine($"Time = {t:F3} сек");
            Console.WriteLine($"MFLOPS = {p:F2}\n");
        }
    }
}

/*using System;
using System.Diagnostics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;

namespace laboratory_work_2
{
    internal class Program
    {
        const int N = 4096;
        static void Main()
        {
            Console.WriteLine($"Размер матриц: {N}x{N}\n");

            float[,] A = new float[N, N];
            float[,] B = new float[N, N];
            float[,] C = new float[N, N];

            Random rnd = new Random();

            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    A[i, j] = (float)rnd.NextDouble();
                    B[i, j] = (float)rnd.NextDouble();
                }

            double c = 2.0 * N * N * N;

            // Обычный 
            var sw = Stopwatch.StartNew();
            MultiplyClassic(A, B, C);
            sw.Stop();
            PrintStats("Classic", sw.Elapsed.TotalSeconds, c);

            // BLAS (MathNet) 
            var mA = DenseMatrix.OfArray(A);
            var mB = DenseMatrix.OfArray(B);

            sw.Restart();
            var mC = mA * mB;
            sw.Stop();
            PrintStats("BLAS (MathNet)", sw.Elapsed.TotalSeconds, c);

            // Оптимизированный 
            sw.Restart();
            MultiplyBlocked(A, B, C, 64);
            sw.Stop();
            PrintStats("Blocked", sw.Elapsed.TotalSeconds, c);

            Console.WriteLine("\nВыполнил:");
            Console.WriteLine("Коровин Валерий Александрович");
            Console.WriteLine("Группа: 090301-ПОВв-з24");
        }

        static void MultiplyClassic(float[,] A, float[,] B, float[,] C)
        {
            int n = A.GetLength(0);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < n; k++)
                        sum += A[i, k] * B[k, j];
                    C[i, j] = sum;
                }
        }

        static void MultiplyBlocked(float[,] A, float[,] B, float[,] C, int block)
        {
            int n = A.GetLength(0);

            for (int ii = 0; ii < n; ii += block)
                for (int jj = 0; jj < n; jj += block)
                    for (int kk = 0; kk < n; kk += block)
                        for (int i = ii; i < ii + block && i < n; i++)
                            for (int j = jj; j < jj + block && j < n; j++)
                            {
                                float sum = C[i, j];
                                for (int k = kk; k < kk + block && k < n; k++)
                                    sum += A[i, k] * B[k, j];
                                C[i, j] = sum;
                            }
        }

        static void PrintStats(string name, double t, double c)
        {
            double p = c / t * 1e-6;
            Console.WriteLine($"{name}:");
            Console.WriteLine($"Time = {t:F3} сек");
            Console.WriteLine($"MFLOPS = {p:F2}\n");
        }
    }
}*/