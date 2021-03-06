﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MediaSet.Import
{
    class Program
    {
        static void Main()
        {
            var exit = false;

            while (!exit)
            {
                Console.WriteLine("Media type to import?");
                var mediaType = Console.ReadLine();

                if (mediaType == "exit")
                {
                    Console.WriteLine("Exiting");
                    exit = true;
                    continue;
                }
                Console.WriteLine("Location of file?");
                var fileLocation = Console.ReadLine();


                //var file = @"C:\Users\pfischer\Documents\MovieExport.txt";
                Console.WriteLine("Importing data for media type {0} from file {1}", mediaType, fileLocation);
                IList<string> dataRows = File.ReadAllLines(fileLocation).ToList();

                switch (mediaType.ToLower())
                {
                    case "movies":
                        MovieImport.Import(dataRows);
                        break;

                    case "books":
                        BookImport.Import(dataRows);
                        break;

                    case "games":
                        GameImport.Import(dataRows);
                        break;
                }

                Console.WriteLine("Done Importing {0} from {1}", mediaType, fileLocation);
            }
        }
    }
}
