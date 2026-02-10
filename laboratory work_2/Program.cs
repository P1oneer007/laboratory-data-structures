using System;
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
}