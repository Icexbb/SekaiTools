using SekaiDataFetch;
using SekaiDataFetch.List;


namespace Test
{
    public static class Program
    {
        public static void Main()
        {
            var name = "1";
            var age = 24;
            var template = "My name is {name} and I am {age} years old.";
            var result = string.Format(template, new {name, age});
            Console.WriteLine(result);
        }
    }
}