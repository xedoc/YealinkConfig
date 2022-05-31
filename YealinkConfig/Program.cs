using System;

namespace YealinkConfig
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if ( args.Length >= 3 )
            {
                var yealink = new Yealink(args[0], args[1], args[2]);
                if( yealink.Login() )
                {
                    if (yealink.Upload(args[3]))
                        return 0;
                    else
                        return 3;
                }
                else
                {
                    return 5;
                }
            } 
            else
            {
                Console.WriteLine("Usage: YealinkConfig.exe PhoneIP UserName Password ConfigFilePath");
                Console.WriteLine(@"Example: YealinkConfig.exe 192.168.0.5 admin admin ""c:\config.cfg""");
                Console.WriteLine("Result (errorlevel): 0 - config uploaded, 1 - other error, 3 - upload error, 5 - login error");
            }
            return 1;
        }
    }
}
