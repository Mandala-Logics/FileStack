using System;
using System.Collections.Generic;
using System.Linq;

namespace MandalaLogics.Command
{
    public enum CommandAnswer { Null, Yes, No }

    public static partial class CommandHelper
    {
        public static CommandAnswer AskYesNoQuestion(string question)
        {
            Console.WriteLine();

            Console.WriteLine(question);

            do
            {

                Console.Write("Yes/No: ");

                var ans = Console.ReadLine().Trim().ToLower();

                if (ans.Equals("no") || ans.Equals("n"))
                {
                    return CommandAnswer.No;
                }
                else if (ans.Equals("yes") || ans.Equals("y"))
                {
                    return CommandAnswer.Yes;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Please answer yes or no.");
                }

            } while (true);
        }

        public static int AskListQuestion<T>(string question, IEnumerable<T> list)
        {
            return AskListQuestion(question, list.ToList());
        }

        public static int AskListQuestion<T>(string question, IReadOnlyList<T> list)
        {
            if (list.Count == 0) { throw new ArgumentException("List cannot be empty."); }

            Console.WriteLine();

            Console.WriteLine(question);

            Console.WriteLine();

            int x = 1;

            foreach (var obj in list)
            {
                Console.WriteLine($"[{x}]: {obj}");
                x++;
            }

            do
            {
                Console.WriteLine();

                Console.Write($"Select option [1-{list.Count}]: ");

                var ans = Console.ReadLine();

                if (int.TryParse(ans, out int i) && i > 0 && i <= list.Count) { return i - 1; }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine($"Please answer a number between 1 and {list.Count}.");
                }

            } while (true);
        }
    }
}