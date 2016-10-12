using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using LocalLib;

namespace MaiRepacker
{
    static class Program
    {
        const int FileSizeThreshold = 5 * 1024 * 1024;

        static byte[] header_dummy = { 0, 0, 0, 0 };

        static string path_input = null;
        static string path_output = null;
        static string path_work = null;

        static string GetDirectoryEntryName(string path)
        {
            return path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar));
        }

        static string ConvertBinaryToHex(byte[] arr)
        {
            var sb = new StringBuilder();
            foreach (var t in arr)
                sb.AppendFormat("{0:X2}", t);
            return sb.ToString();
        }

        static void ProcessSafeMode(string path)
        {
            using(var fs = new FileStream(path, FileMode.Open))
            {
                fs.Position = 0x80;

                if (fs.ReadByte() == 1)
                {
                    fs.Position--;
                    fs.WriteByte(2);
                    Console.WriteLine("Converted as safe mode: " + path);
                }
            }
        }

        static void ProcessEachFile(string dir)
        {
            //Console.WriteLine("Working directory: " + dir.Substring(Environment.CurrentDirectory.Length));
            int remaining_entry = 0;

            foreach (var d in Directory.GetDirectories(dir))
            {
                ProcessEachFile(d);
                remaining_entry += 1;
            }

            foreach (var f in Directory.GetFiles(dir))
            {
                if (f.Contains("eboot.bin") || f.Contains("eboot_origin.bin"))
                {
                    ProcessSafeMode(f);
                    return;
                }

                var info = new FileInfo(f);
                if (info.Length > FileSizeThreshold)
                {
                    var t = path_work + f.Substring(dir.Length);
                    File.Move(f, t);
                }
                else
                    remaining_entry += 1;
            }

            if(remaining_entry == 0)
            {
                string path = null;
                for (int u = 0; u < 10; ++u) {
                    path = dir + Path.DirectorySeparatorChar + "maiph" + u;
                    if (File.Exists(path))
                        path = null;
                    else
                        break;
                }

                if (path == null) throw new Exception("Failed to create placeholder.");

                using (var fs = new FileStream(path, FileMode.CreateNew))
                {
                    fs.Write(header_dummy, 0, 4);
                }
            }
        }

        static void CloneDirectoryStructure(string src, string dst)
        {
            foreach(var d in Directory.GetDirectories(src))
            {
                var t = dst + Path.DirectorySeparatorChar + GetDirectoryEntryName(d);
                Directory.CreateDirectory(t);
                CloneDirectoryStructure(d, t);
            }
        }

        static void ProcessDirectory(string dir)
        {
            byte[] info = null;
            using (var fs = new FileStream(dir + Path.DirectorySeparatorChar + "sce_sys" + Path.DirectorySeparatorChar + "param.sfo", FileMode.Open))
            {
                using (var buf = new MemoryStream())
                {
                    fs.Seek(-20, SeekOrigin.End);
                    fs.CopyTo(buf);
                    info = buf.ToArray();
                }
            }

            var app_id = Encoding.ASCII.GetString(info, 0, 9);
            var dst = path_output + Path.DirectorySeparatorChar + app_id;
            var dst_zip = path_output + Path.DirectorySeparatorChar + app_id + ".zip";
            Console.WriteLine("App Id: " + app_id);

            Console.WriteLine("Processing...");
            Directory.Delete(dir + Path.DirectorySeparatorChar + "sce_sys" + Path.DirectorySeparatorChar + "manual", true);
            
            Directory.CreateDirectory(path_work);
            while (!Directory.Exists(path_work)) { Thread.Sleep(100); }
            CloneDirectoryStructure(dir, path_work);

            foreach (var d in Directory.GetDirectories(dir))
            {
                if (d.Contains("sce_module")) continue;
                if (d.Contains("sce_sys")) continue;
                ProcessEachFile(d);
            }

            Console.WriteLine("Packing...");
            Directory.Move(path_work, dst);
            while (Directory.Exists(path_work)) { Thread.Sleep(100); }
            ZipUtil.CreateFromDirectory(dir, dst_zip);
            while(!File.Exists(dst_zip)) { Thread.Sleep(100); }
            Directory.Delete(dir, true);
        }
        
        static void Main(string[] args)
        {
            path_work = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "work";
            path_input = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "input";
            path_output = Environment.CurrentDirectory + Path.DirectorySeparatorChar + "output";

            if (!Directory.Exists(path_input))
            {
                Console.WriteLine("Directory vpk is not found.");
                return;
            }

            if (Directory.Exists(path_output))
            {
                Directory.Delete(path_output, true);
                while (Directory.Exists(path_output)) { Thread.Sleep(100); }
            }

            Directory.CreateDirectory(path_output);
            while (!Directory.Exists(path_output)) { Thread.Sleep(100); }
            
            foreach (var t in Directory.GetDirectories(path_input))
                ProcessDirectory(t);

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey(true);
        }
    }
}
