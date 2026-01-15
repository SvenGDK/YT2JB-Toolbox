using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentFTP;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using static YT2JB_Toolbox.PS5ParamClass;

namespace YT2JB_Toolbox
{
    public partial class MainWindow : Window
    {

        private ScrollViewer? LogTextBoxScrollViewer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            // Init SQLite
            SQLitePCL.Batteries_V2.Init();
        }

        private void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LogTextBoxScrollViewer = LogTextBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }

        private async void PatchLocalFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var FBD = new OpenFolderDialog() { Title = "Select a folder that contains app.db, appinfo.db and param.json" };
            var FBDResult = await FBD.ShowAsync(this);

            if (FBDResult != null)
            {
                int totalRows = 0;
                LogTextBox.Clear();

                LogTextBox.Text += "Starting to patch appinfo.db ...";
                LogTextBoxScrollViewer?.ScrollToEnd();

                if (File.Exists(Path.Combine(FBDResult, "appinfo.db")))
                {
                    // Patch appinfo.db
                    using var connection = new SqliteConnection($"Data Source={Path.Combine(FBDResult, "appinfo.db")}");
                    connection.Open();

                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Update tbl_appinfo columns
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = transaction;

                            cmd.CommandText = @"
                    UPDATE tbl_appinfo 
                    SET val = $contentVersion 
                    WHERE titleId = $titleId 
                    AND key = 'CONTENT_VERSION';
                ";
                            cmd.Parameters.AddWithValue("$contentVersion", "99.999.999");
                            cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                            totalRows += cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();

                            cmd.CommandText = @"
                    UPDATE tbl_appinfo 
                    SET val = $versionFileUri 
                    WHERE titleId = $titleId 
                    AND key = 'VERSION_FILE_URI';
                ";
                            cmd.Parameters.AddWithValue("$versionFileUri", "http://127.0.0.2");
                            cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                            totalRows += cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        try
                        {
                            LogTextBox.Text += "An error occured, rolling back changes made to appinfo.db\n";
                            LogTextBoxScrollViewer?.ScrollToEnd();
                            transaction.Rollback();
                        }
                        catch
                        {
                            LogTextBox.Text += "Failed to rollback appinfo.db changes!\n";
                            LogTextBoxScrollViewer?.ScrollToEnd();
                        }
                        throw;
                    }
                    finally
                    {
                        connection.Close();
                        LogTextBox.Text += $"appinfo.db patched - {totalRows} changes done.\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                else
                {
                    LogTextBox.Text += "File: appinfo.db not found and will not be patched!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                totalRows = 0;

                LogTextBox.Text += "Starting to patch app.db ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                if (File.Exists(Path.Combine(FBDResult, "app.db")))
                {
                    // Patch app.db
                    using var NewConnection = new SqliteConnection($"Data Source={Path.Combine(FBDResult, "app.db")}");
                    NewConnection.Open();

                    using var NewTransaction = NewConnection.BeginTransaction();
                    try
                    {
                        // Update JSON inside tbl_contentinfo.AppInfoJson using json_set
                        using (var cmd = NewConnection.CreateCommand())
                        {
                            cmd.Transaction = NewTransaction;
                            cmd.CommandText = @"
                    UPDATE tbl_contentinfo
                    SET AppInfoJson = json_set(
                        AppInfoJson,
                        '$.CONTENT_VERSION', $contentVersion,
                        '$.VERSION_FILE_URI', $versionFileUri
                    )
                    WHERE titleId = $titleId;
                ";
                            cmd.Parameters.AddWithValue("$contentVersion", "99.999.999");
                            cmd.Parameters.AddWithValue("$versionFileUri", "http://127.0.0.2");
                            cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                            totalRows += cmd.ExecuteNonQuery();
                        }

                        NewTransaction.Commit();
                    }
                    catch
                    {
                        try
                        {
                            LogTextBox.Text += "An error occured, rolling back changes made to app.db\n";
                            LogTextBoxScrollViewer?.ScrollToEnd();
                            NewTransaction.Rollback();
                        }
                        catch
                        {
                            LogTextBox.Text += "Failed to rollback app.db changes!\n";
                            LogTextBoxScrollViewer?.ScrollToEnd();
                        }
                        throw;
                    }
                    finally
                    {
                        NewConnection.Close();
                        LogTextBox.Text += $"app.db patched - {totalRows} changes done.\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                else
                {
                    LogTextBox.Text += "File: app.db not found and will not be patched!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += "Starting to patch param.json ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                // Patch param.json
                if (File.Exists(Path.Combine(FBDResult, "param.json")))
                {
                    try
                    {
                        string JSONData = File.ReadAllText(Path.Combine(FBDResult, "param.json"));
                        if (JSONData != null)
                        {
                            PS5Param ParamData = JsonConvert.DeserializeObject<PS5Param>(JSONData)!;

                            ParamData.ContentVersion = "99.999.999";
                            ParamData.VersionFileUri = "http://127.0.0.2";

                            string RawDataJSON = JsonConvert.SerializeObject(ParamData, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                            File.WriteAllText(Path.Combine(FBDResult, "param.json"), RawDataJSON);
                        }
                        else
                        {
                            LogTextBox.Text += "Could not read param.json!\n";
                            LogTextBoxScrollViewer?.ScrollToEnd();
                        }
                    }
                    catch
                    {
                        LogTextBox.Text += "Failed to modify param.json!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                else
                {
                    LogTextBox.Text += "File: param.json not found and will not be patched!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += "Done! All found files have been patched and ready to use.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
            else
            {
                LogTextBox.Text += "No folder selected.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

        private async void PatchFTPFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PS5IPTextBox.Text) && !string.IsNullOrEmpty(PS5FTPPortTextBox.Text))
            {
                LogTextBox.Clear();

                LogTextBox.Text += "Getting :\n";
                LogTextBox.Text += "/system_data/priv/mms/appinfo.db\n";
                LogTextBox.Text += "/system_data/priv/mms/app.db\n";
                LogTextBox.Text += "/user/appmeta/PPSA01650/param.json\n";
                LogTextBox.Text += "Please wait ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Cache")))
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Cache"));

                try
                {
                    IEnumerable<string> RemoteFiles = [
                        "/system_data/priv/mms/appinfo.db",
                        "/system_data/priv/mms/app.db",
                        "/user/appmeta/PPSA01650/param.json"
                        ];

                    // Configurate AsyncFtpClient
                    using var conn = new AsyncFtpClient(PS5IPTextBox.Text, "anonymous", "anonymous", Convert.ToInt32(PS5FTPPortTextBox.Text));
                    conn.Config.EncryptionMode = FtpEncryptionMode.None;
                    conn.Config.SslProtocols = SslProtocols.None;
                    conn.Config.DataConnectionEncryption = false;

                    // Connect
                    await conn.Connect();

                    // Get required files
                    await conn.DownloadFiles(Path.Combine(Environment.CurrentDirectory, "Cache"), RemoteFiles, FtpLocalExists.Overwrite, FtpVerify.None, FtpError.None);

                    // Temporary disconnect
                    await conn.Disconnect();
                }
                catch (Exception)
                {
                    LogTextBox.Text += "Could not get the appinfo.db file, please verify your connection.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += $"All files saved to. {Path.Combine(Environment.CurrentDirectory, "Cache")}\n";
                LogTextBox.Text += "Creating a backup of all files ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                File.Copy(Path.Combine(Environment.CurrentDirectory, "Cache", "appinfo.db"), Path.Combine(Environment.CurrentDirectory, "Cache", "appinfo_backup.db"), true);
                File.Copy(Path.Combine(Environment.CurrentDirectory, "Cache", "app.db"), Path.Combine(Environment.CurrentDirectory, "Cache", "app_backup.db"), true);
                File.Copy(Path.Combine(Environment.CurrentDirectory, "Cache", "param.json"), Path.Combine(Environment.CurrentDirectory, "Cache", "param_backup.json"), true);

                LogTextBox.Text += "Backup done!\n";
                LogTextBox.Text += "Starting to patch all files ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                int totalRows = 0;

                LogTextBox.Text += "Starting to patch appinfo.db ...";
                LogTextBoxScrollViewer?.ScrollToEnd();

                // Patch appinfo.db
                using var connection = new SqliteConnection($"Data Source={Path.Combine(Environment.CurrentDirectory, "Cache", "appinfo.db")}");
                connection.Open();

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Update tbl_appinfo columns
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;

                        cmd.CommandText = @"
                    UPDATE tbl_appinfo 
                    SET val = $contentVersion 
                    WHERE titleId = $titleId 
                    AND key = 'CONTENT_VERSION'
                ";
                        cmd.Parameters.AddWithValue("$contentVersion", "99.999.999");
                        cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                        totalRows += cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();

                        cmd.CommandText = @"
                    UPDATE tbl_appinfo 
                    SET val = $versionFileUri 
                    WHERE titleId = $titleId 
                    AND key = 'VERSION_FILE_URI'
                ";
                        cmd.Parameters.AddWithValue("$versionFileUri", "http://127.0.0.2");
                        cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                        totalRows += cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    try
                    {
                        LogTextBox.Text += "An error occured, rolling back changes made to appinfo.db\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                        transaction.Rollback();
                    }
                    catch
                    {
                        LogTextBox.Text += "Failed to rollback appinfo.db changes!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                    throw;
                }
                finally
                {
                    connection.Close();
                    LogTextBox.Text += $"appinfo.db patched - {totalRows} changes done.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                totalRows = 0;

                LogTextBox.Text += "Starting to patch app.db ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                // Patch app.db
                using var NewConnection = new SqliteConnection($"Data Source={Path.Combine(Environment.CurrentDirectory, "Cache", "app.db")}");
                connection.Open();

                using var NewTransaction = NewConnection.BeginTransaction();
                try
                {
                    // Update JSON inside tbl_contentinfo.AppInfoJson using json_set
                    using (var cmd = NewConnection.CreateCommand())
                    {
                        cmd.Transaction = NewTransaction;
                        cmd.CommandText = @"
                    UPDATE tbl_contentinfo
                    SET AppInfoJson = json_set(
                        AppInfoJson,
                        '$.CONTENT_VERSION', $contentVersion,
                        '$.VERSION_FILE_URI', $versionFileUri
                    )
                    WHERE titleId = $titleId;
                ";
                        cmd.Parameters.AddWithValue("$contentVersion", "99.999.999");
                        cmd.Parameters.AddWithValue("$versionFileUri", "http://127.0.0.2");
                        cmd.Parameters.AddWithValue("$titleId", "PPSA01650");

                        totalRows += cmd.ExecuteNonQuery();
                    }

                    NewTransaction.Commit();
                }
                catch
                {
                    try
                    {
                        LogTextBox.Text += "An error occured, rolling back changes made to app.db\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                        NewTransaction.Rollback();
                    }
                    catch
                    {
                        LogTextBox.Text += "Failed to rollback app.db changes!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                    throw;
                }
                finally
                {
                    NewConnection.Close();
                    LogTextBox.Text += $"app.db patched - {totalRows} changes done.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += "Starting to patch param.json ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                // Patch param.json
                try
                {
                    string JSONData = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "Cache", "param.json"));
                    if (JSONData != null)
                    {
                        PS5Param ParamData = JsonConvert.DeserializeObject<PS5Param>(JSONData)!;

                        ParamData.ContentVersion = "99.999.999";
                        ParamData.VersionFileUri = "http://127.0.0.2";

                        string RawDataJSON = JsonConvert.SerializeObject(ParamData, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                        File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "Cache", "param.json"), RawDataJSON);
                    }
                    else
                    {
                        LogTextBox.Text += "Could not read param.json!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                catch
                {
                    LogTextBox.Text += "Failed to modify param.json!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += "All files patched successfully!\n";

                LogTextBox.Text += "Now uploading all files back ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                // Upload back
                try
                {
                    using var conn = new AsyncFtpClient(PS5IPTextBox.Text, "anonymous", "anonymous", Convert.ToInt32(PS5FTPPortTextBox.Text));
                    conn.Config.EncryptionMode = FtpEncryptionMode.None;
                    conn.Config.SslProtocols = SslProtocols.None;
                    conn.Config.DataConnectionEncryption = false;

                    // Connect
                    await conn.Connect();

                    // Upload and replace all files
                    await conn.UploadFile(Path.Combine(Environment.CurrentDirectory, "Cache", "appinfo.db"), "/system_data/priv/mms/appinfo.db", FtpRemoteExists.OverwriteInPlace, false, FtpVerify.None);
                    await conn.UploadFile(Path.Combine(Environment.CurrentDirectory, "Cache", "app.db"), "/system_data/priv/mms/app.db", FtpRemoteExists.OverwriteInPlace, false, FtpVerify.None);
                    await conn.UploadFile(Path.Combine(Environment.CurrentDirectory, "Cache", "param.json"), "/system_data/priv/appmeta/PPSA01650/param.json", FtpRemoteExists.OverwriteInPlace, false, FtpVerify.None);
                    await conn.UploadFile(Path.Combine(Environment.CurrentDirectory, "Cache", "param.json"), "/user/appmeta/PPSA01650/param.json", FtpRemoteExists.OverwriteInPlace, false, FtpVerify.None);

                    // Disconnect
                    await conn.Disconnect();
                }
                catch (Exception)
                {
                    LogTextBox.Text += "Could not upload the files back to the PS5, please verify your connection.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }

                LogTextBox.Text += "Done updating all files on the PS5.\nPress the PS button on the controller and reboot !";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
            else
            {
                LogTextBox.Text += "Please enter an IP Address and FTP Port first.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

        private async void AutoReplaceDownload0dat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PS5IPTextBox.Text) && !string.IsNullOrEmpty(PS5FTPPortTextBox.Text))
            {
                LogTextBox.Clear();
                LogTextBox.Text += "Getting latest download0.dat ...\n";
                LogTextBoxScrollViewer?.ScrollToEnd();

                if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Cache")))
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Cache"));

                if (File.Exists(Path.Combine(Environment.CurrentDirectory, "Cache", "download0.dat")))
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "Cache", "download0.dat"));

                try
                {
                    // Download latest download0.dat file
                    using (var http = new HttpClient())
                    using (var response = await http.GetAsync("https://github.com/itsPLK/ps5_y2jb_autoloader/releases/latest/download/download0.dat", HttpCompletionOption.ResponseHeadersRead, default))
                    {
                        response.EnsureSuccessStatusCode();

                        using var sourceStream = await response.Content.ReadAsStreamAsync(default);
                        using var destinationStream = File.Create(Path.Combine(Environment.CurrentDirectory, "Cache", "download0.dat"));
                        await sourceStream.CopyToAsync(destinationStream, 81920, default);
                    }

                    LogTextBox.Text += "Retrieved download0.dat. Now replacing on the PS5 ...\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();

                    // Connect to FTP server and replace
                    using var conn = new AsyncFtpClient(PS5IPTextBox.Text, "anonymous", "anonymous", Convert.ToInt32(PS5FTPPortTextBox.Text));
                    conn.Config.EncryptionMode = FtpEncryptionMode.None;
                    conn.Config.SslProtocols = SslProtocols.None;
                    conn.Config.DataConnectionEncryption = false;

                    // Connect
                    await conn.Connect();

                    // Check if download0.dat still exists and pass download0datDidNotExist for conn.UploadFile's createRemoteDir = true if not
                    bool download0datDidNotExist = true;
                    if (await conn.FileExists("/user/download/PPSA01650/download0.dat"))
                    {
                        // Remove the old download0.dat
                        await conn.DeleteFile("/user/download/PPSA01650/download0.dat");
                        download0datDidNotExist = false;
                    }

                    // Upload new download0.dat file
                    var UploadStatus = await conn.UploadFile(Path.Combine(Environment.CurrentDirectory, "Cache", "download0.dat"), "/user/download/PPSA01650/download0.dat", FtpRemoteExists.OverwriteInPlace, download0datDidNotExist, FtpVerify.None);

                    // Disconnect
                    await conn.Disconnect();

                    LogTextBox.Text += "Replacing download0.dat succeeded!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }
                catch
                {
                    LogTextBox.Text += "Failed to replace download0.dat on the PS5!\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }
            }
            else
            {
                LogTextBox.Text += "Please enter an IP Address and FTP Port first.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

        private async void UploadDownload0dat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PS5IPTextBox.Text) && !string.IsNullOrEmpty(PS5FTPPortTextBox.Text))
            {
                var datFileFilter = new FileDialogFilter
                {
                    Name = "dat File",
                    Extensions = ["dat"]
                };
                var OFD = new OpenFileDialog() { Title = "Select an appinfo.db file", Filters = { datFileFilter }, AllowMultiple = false };
                var OFDResult = await OFD.ShowAsync(this);

                if (OFDResult != null && OFDResult.Length > 0)
                {
                    LogTextBox.Clear();
                    LogTextBox.Text += "Uploading selected download0.dat file, please wait ...\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();

                    try
                    {
                        // Connect to FTP server and replace
                        using var conn = new AsyncFtpClient(PS5IPTextBox.Text, "anonymous", "anonymous", Convert.ToInt32(PS5FTPPortTextBox.Text));
                        conn.Config.EncryptionMode = FtpEncryptionMode.None;
                        conn.Config.SslProtocols = SslProtocols.None;
                        conn.Config.DataConnectionEncryption = false;

                        // Connect
                        await conn.Connect();

                        // Check if download0.dat still exists and pass download0datDidNotExist for conn.UploadFile's createRemoteDir = true if not
                        bool download0datDidNotExist = true;
                        if (await conn.FileExists("/user/download/PPSA01650/download0.dat"))
                        {
                            // Remove the old download0.dat
                            await conn.DeleteFile("/user/download/PPSA01650/download0.dat");
                            download0datDidNotExist = false;
                        }

                        // Upload new download0.dat file
                        var UploadStatus = await conn.UploadFile(OFDResult[0], "/user/download/PPSA01650/download0.dat", FtpRemoteExists.OverwriteInPlace, download0datDidNotExist, FtpVerify.None);

                        // Disconnect
                        await conn.Disconnect();

                        LogTextBox.Text += "Replacing download0.dat succeeded!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                    catch
                    {
                        LogTextBox.Text += "Failed to replace download0.dat on the PS5!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
            }
            else
            {
                LogTextBox.Text += "Please enter an IP Address and FTP Port first.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

        private async void InstallYouTubePKG_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PS5IPTextBox.Text))
            {
                // Check if Direct Package Installer V2 is service is active
                bool isActive = false;
                using var NewTcpClient = new TcpClient();
                try
                {
                    var NewIAsyncResult = NewTcpClient.BeginConnect(PS5IPTextBox.Text, 12800, null, null);
                    bool PortOpen = NewIAsyncResult.AsyncWaitHandle.WaitOne(1000);

                    if (!PortOpen)
                    {
                        isActive = false;
                    }

                    NewTcpClient.EndConnect(NewIAsyncResult);
                    isActive = true;
                }
                catch (Exception)
                {
                    isActive = false;
                }

                // Send PKG if active
                if (isActive)
                {
                    LogTextBox.Clear();
                    LogTextBox.Text += "YouTube PKG has been send to the PS5.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();

                    string YTPKGURL = "http://87.106.5.21/ps5/hb/UP4381-PPSA01650_00-YOUTUBESIEA00000.pkg";

                    using var NewHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    var PS5RequestURL = $"http://{PS5IPTextBox.Text}:12800/upload";
                    var Boundary = "----DirectPackageInstallerBoundary";
                    using var NewMultipartFormDataContent = new MultipartFormDataContent(Boundary) { { new StringContent(string.Empty), "\"file\"", "\"\"" } };

                    using var NewMemoryStream = new MemoryStream(Encoding.UTF8.GetBytes(YTPKGURL));
                    NewMultipartFormDataContent.Add(new StreamContent(NewMemoryStream), "\"url\"");

                    var Response = await NewHttpClient.PostAsync(PS5RequestURL, NewMultipartFormDataContent);

                    using var NewMS = new MemoryStream();
                    await Response.Content.CopyToAsync(NewMS);

                    var Result = Encoding.UTF8.GetString(NewMS.ToArray());

                    if (Result.Contains("SUCCESS:"))
                    {
                        LogTextBox.Text += "Success! YouTube PKG is now installing.\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                    else
                    {
                        LogTextBox.Text += "Failed to install the YouTube PKG!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                else
                {
                    LogTextBox.Text += "Please enable the Direct Package Installer V2 Service first in etaHEN.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }
            }
            else
            {
                LogTextBox.Text += "Please enter an IP Address first.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

        private async void SendetaHENPayload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PS5IPTextBox.Text) && !string.IsNullOrEmpty(PS5PayloadPortTextBox.Text))
            {
                // Download latest etaHEN to Cache
                LogTextBox.Clear();

                if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Cache")))
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Cache"));

                // Download latest etaHEN-2.5B.bin file
                if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "Cache", "etaHEN-2.5B.bin")))
                {
                    LogTextBox.Text += "Getting latest etaHEN v2.5B ...\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();

                    try
                    {
                        using var http = new HttpClient();
                        using var response = await http.GetAsync("https://github.com/etaHEN/etaHEN/releases/download/2.5B/etaHEN-2.5B.bin", HttpCompletionOption.ResponseHeadersRead, default);
                        response.EnsureSuccessStatusCode();

                        using var sourceStream = await response.Content.ReadAsStreamAsync(default);
                        using var destinationStream = File.Create(Path.Combine(Environment.CurrentDirectory, "Cache", "etaHEN-2.5B.bin"));
                        await sourceStream.CopyToAsync(destinationStream, 81920, default);
                    }
                    catch
                    {
                        LogTextBox.Text += "An error occured while downloading the etaHEN-2.5B.bin payload.\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }

                // Send etaHEN payload to PS5
                try
                {
                    if (File.Exists(Path.Combine(Environment.CurrentDirectory, "Cache", "etaHEN-2.5B.bin")))
                    {
                        LogTextBox.Text += "Sending etaHEN-2.5B.bin to the PS5 ...\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();

                        Socket SenderSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                        {
                            ReceiveTimeout = 3000,
                            SendTimeout = 3000
                        };

                        await SenderSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(PS5IPTextBox.Text), Convert.ToInt32(PS5PayloadPortTextBox.Text)));
                        await SenderSocket.SendFileAsync(Path.Combine(Environment.CurrentDirectory, "Cache", "etaHEN-2.5B.bin"));
                        SenderSocket.Close();

                        LogTextBox.Text += "Payload etaHEN-2.5B.bin send successfully!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                    else
                    {
                        LogTextBox.Text += "Could not find the downloaded etaHEN-2.5B.bin payload!\n";
                        LogTextBoxScrollViewer?.ScrollToEnd();
                    }
                }
                catch
                {
                    LogTextBox.Text += "An error occured while sending the etaHEN payload.\n";
                    LogTextBoxScrollViewer?.ScrollToEnd();
                }
            }
            else
            {
                LogTextBox.Text += "Please enter an IP Address and Payload Port first.\n";
                LogTextBoxScrollViewer?.ScrollToEnd();
            }
        }

    }
}