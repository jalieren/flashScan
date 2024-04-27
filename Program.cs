using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

class FlashDriveScanner
{
    private static string tempFolderPath = Path.GetTempPath() + @"\flashFiles";
    private static string rememberedDriveFilePath = "rememberedDrives.txt";

    static void Main()
    {
        if (!Directory.Exists(tempFolderPath))
            Directory.CreateDirectory(tempFolderPath);

        while (true)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
            foreach (ManagementObject disk in searcher.Get())
            {
                string driveSerialNumber = disk["SerialNumber"]?.ToString();
                if (!IsSerialNumberRemembered(driveSerialNumber))
                {
                    string driveModel = disk["Model"]?.ToString();

                    string driveLetter = GetDriveLetterFromDisk(disk);
                    if (!string.IsNullOrEmpty(driveLetter))
                    {
                        ScanAndCopyFiles(driveLetter, driveSerialNumber, driveModel);
                        RememberSerialNumber(driveSerialNumber);
                    }
                }
                else
                {
                    string driveLetter = GetDriveLetterFromDisk(disk);
                    if (!string.IsNullOrEmpty(driveLetter))
                    {
                        CheckForChangesAndCopy(driveLetter, driveSerialNumber);
                    }
                }
            }

            Thread.Sleep(5000); // Пауза в 5 секунд между сканированиями
        }
    }

    private static string GetDriveLetterFromDisk(ManagementObject disk)
    {
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{disk["DeviceID"]}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
            foreach (ManagementObject partition in searcher.Get())
            {
                searcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                foreach (ManagementObject logicalDisk in searcher.Get())
                {
                    return logicalDisk["DeviceID"]?.ToString();
                }
            }
            return null;
        }
        catch (Exception e) 
        { 
            Console.WriteLine(e);
            return null;
        }
    }

    // проверка на наличие серийника в списке
    private static bool IsSerialNumberRemembered(string serialNumber)
    {
        if (File.Exists(rememberedDriveFilePath))
        {
            string[] rememberedSerialNumbers = File.ReadAllLines(rememberedDriveFilePath);
            return Array.Exists(rememberedSerialNumbers, s => s == serialNumber);
        }
        return false;
    }

    // запоминание серийника
    private static void RememberSerialNumber(string serialNumber)
    {
        using (StreamWriter writer = new StreamWriter(rememberedDriveFilePath, true))
        {
            writer.WriteLine(serialNumber);
        }
    }

    // копируем
    private static void ScanAndCopyFiles(string drive, string serialNumber, string driveModel)
    {
        try
        {
            string sourcePath = drive + @"\";
            string[] files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            //.Where(file => new FileInfo(file).Length < 800 * 1024 * 1024) // Файлы менее 800 МБ
            //.ToArray();
            // сверху копирование всех файлов, можно самостоятельно поставить ограничение


            string destinationFolderPath = Path.Combine(tempFolderPath, serialNumber);

            foreach (string file in files)
            {
                string relativePath = file.Substring(sourcePath.Length);
                string destinationPath = Path.Combine(destinationFolderPath, relativePath);

                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Copy(file, destinationPath, true);

                // Логируем информацию о скопированном файле
                LogFileCopyInfo(serialNumber, driveModel, relativePath);
            }
        }
        catch (Exception e) { Console.WriteLine(e); }
    }

    // копируем если есть изменения
    private static void CheckForChangesAndCopy(string drive, string serialNumber)
    {
        try
        {
            string sourcePath = drive + @"\";
            string destinationFolderPath = Path.Combine(tempFolderPath, serialNumber);

            string[] existingFiles = Directory.GetFiles(destinationFolderPath, "*", SearchOption.AllDirectories);

            string[] newFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            //.Where(file => new FileInfo(file).Length < 800 * 1024 * 1024) // Файлы менее 800 МБ
            //.ToArray();
            // сверху копирование всех файлов, можно самостоятельно поставить ограничение

            foreach (string file in newFiles)
            {
                string relativePath = file.Substring(sourcePath.Length);
                string destinationPath = Path.Combine(destinationFolderPath, relativePath);

                if (!existingFiles.Contains(destinationPath))
                {
                    string destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDirectory))
                        Directory.CreateDirectory(destinationDirectory);

                    File.Copy(file, destinationPath, true);

                    // Логируем информацию о скопированном файле
                    LogFileCopyInfo(serialNumber, null, relativePath);
                }
            }
        }
        catch (Exception e) { Console.WriteLine(e); }
    }

    // логируем
    private static void LogFileCopyInfo(string serialNumber, string driveModel, string relativePath)
    {
        string logFilePath = Path.Combine(tempFolderPath + @"\" + serialNumber, "log.txt");

        using (StreamWriter writer = new StreamWriter(logFilePath, true))
        {
            writer.WriteLine($"Флешка: {driveModel ?? "неизвестно"} (Серийный номер: {serialNumber})");
            writer.WriteLine($"Скопирован файл: {relativePath}");
            writer.WriteLine($"Время: {DateTime.Now}");
            writer.WriteLine();
        }
    }
}
