using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Nemiro.OAuth;
using Nemiro.OAuth.LoginForms;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace EncryptionDecrytionProject
{
    public partial class Form1 : Form
    {
        private string CurrentPath = "/";
        private string original = "";
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.AccessToken))
            {
                this.GetAccessToken();
            }
            else
            {
                this.GetFiles();
            }

        }

        private void GetFiles()
        {
            OAuthUtility.GetAsync("https://api.dropboxapi.com/1/metadata/auto/", new HttpParameterCollection {
              {"path",CurrentPath },
            { "access_token",Properties.Settings.Default.AccessToken }
            }, callback: GetFiles_Result
            );
        }
        private void GetFiles_Result(RequestResult result)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<RequestResult>(GetFiles_Result),result);
                return;
            }
            if (result.StatusCode == 200)
            {
                listBox1.Items.Clear();
                listBox1.DisplayMember = "path";    
                foreach (UniValue file in result["contents"])
                {                
                    listBox1.Items.Add(file);
                }
                if (this.CurrentPath != "/")
                {
                    listBox1.Items.Insert(0,UniValue.ParseJson("{path:'..'}"));
                }
            }
            else
            {
                MessageBox.Show("OOHHh Error");
            }

        }
        private void GetAccessToken()
        {
            var login = new DropboxLogin("mqcs31ui448bq3j", "jfr1fi35n2p7xek");
            login.Owner = this;
            login.ShowDialog();
            if (login.IsSuccessfully)
            {
                Properties.Settings.Default.AccessToken = login.AccessToken.Value;
                Properties.Settings.Default.Save();

            }
            else
            {

                MessageBox.Show("Error");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OAuthUtility.PostAsync("https://api.dropboxapi.com/1/fileops/create_folder",
                new HttpParameterCollection
                {
                    {"access_token",Properties.Settings.Default.AccessToken },
                    {"root","auto" },
                    { "path",Path.Combine(this.CurrentPath,textBox1.Text).Replace("\\","/")}
                },callback:CreateFolder_Result
                );
        }
        private void CreateFolder_Result(RequestResult result)
        {

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<RequestResult>(GetFiles_Result), result);
                return;
            }

            if (result.StatusCode == 200)
            {
                this.GetFiles();
            }
            else
            {
                if (result["error"].HasValue)
                {
                    MessageBox.Show(result["error"].ToString());
                }
                else
                {
                    MessageBox.Show("Error");
                }
            }
        }
        
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null)
            {
                return;
            }
            UniValue file = (UniValue)listBox1.SelectedItem;
            if (file["path"]=="..")
            {
                if (this.CurrentPath != "/")
                {
                    this.CurrentPath = Path.GetDirectoryName(this.CurrentPath).Replace("\\", "/");
                }
            }
            else {
                if (file["is_dir"] == 1)
                {
                    this.CurrentPath = file["path"].ToString();
                }
                else
                {
                    saveFileDialog1.FileName = Path.GetFileName(file["path"].ToString());
                    if (saveFileDialog1.ShowDialog()!=System.Windows.Forms.DialogResult.OK) { return; }
                    var web=new WebClient();
                    web.DownloadProgressChanged += DownloadProgressChanged;
                    web.DownloadFile(new Uri(String.Format("https://content.dropboxapi.com/1/files/auto{0}?access_token={1}",file["path"],Properties.Settings.Default.AccessToken)),saveFileDialog1.FileName);
                }
            }
            this.GetFiles();
        }
        private void DownloadProgressChanged(Object sender,DownloadProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }


        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            OAuthUtility.PutAsync("https://content.dropboxapi.com/1/files_put/auto/",
                new HttpParameterCollection
                {
                    {"access_token",Properties.Settings.Default.AccessToken },
                    {"path",Path.Combine(this.CurrentPath,Path.GetFileName(openFileDialog1.FileName)).Replace("\\","/")},
                    { "overwrite",true},
                    {"autorename",true },
                    { openFileDialog1.OpenFile()}

                },
                callback:Upload_Result
                );
        }

        private void Upload_Result(RequestResult result)
        {

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<RequestResult>(Upload_Result), result);
                return;
            }

            if (result.StatusCode == 200)
            {
                this.GetFiles();
            }
            else
            {
                if (result["error"].HasValue)
                {
                    MessageBox.Show(result["error"].ToString());
                }
                else
                {
                    MessageBox.Show("Error");
                }
            }
        }

      /////////////////////////////////Encryption Decryption Part of Code                              ///////////////////
        static byte[] EncryptStringToBytes(string plaintext, byte[] key, byte[] IV)
        {

            //////Check for valid arguments
            if (plaintext == null || plaintext.Length <= 0)
            {
                throw new ArgumentNullException("plaintext");
            }
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException("KEY");
            }
            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("KEY");
            }

            byte[] encrypted;
            // Create an TripleDESCryptoServiceProvider object
            // with the specified key and IV.
            using (TripleDESCryptoServiceProvider tdsAlg = new TripleDESCryptoServiceProvider())
            {
                tdsAlg.Key = key;
                tdsAlg.IV = IV;
                // Create a decrytor to perform the stream transform.
                ICryptoTransform encrypter = tdsAlg.CreateEncryptor(tdsAlg.Key, tdsAlg.IV);
                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encrypter, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(plaintext);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }
        static string DecryptBytesToString(byte[] ciphertext, byte[] key, byte[] IV)
        {
            // Check arguments.
            if (ciphertext == null || ciphertext.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("Key");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an TripleDESCryptoServiceProvider object
            // with the specified key and IV.
            using (TripleDESCryptoServiceProvider tdsAlg = new TripleDESCryptoServiceProvider())
            {
                tdsAlg.Key = key;
                tdsAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = tdsAlg.CreateDecryptor(tdsAlg.Key, tdsAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(ciphertext))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            /////Credit goes to msdn.com
            try
            {

                using (TripleDESCryptoServiceProvider tripledes = new TripleDESCryptoServiceProvider())
                {
                    ////I am Encrpyting the string original to array of bytes
                    byte[] encrypted = EncryptStringToBytes(original, tripledes.Key, tripledes.IV);
                    ////Now its time to roll back the encryption and decryption
                    File.WriteAllBytes("encrypted", encrypted);
                    Byte[] key = tripledes.Key;
                    File.WriteAllBytes("key", key);
                    Byte[] tripledesIV = tripledes.IV;
                    File.WriteAllBytes("tripledesIV", tripledesIV);
                    encrypted = File.ReadAllBytes("encrypted");
                    string roundtrip = DecryptBytesToString(encrypted, key, tripledesIV);
                    MessageBox.Show("Original " + original + " Round trip " + roundtrip);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.StackTrace.ToString());

            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Stream myStream = null;
            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "Text Files (.txt)|*.txt|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = false;   ////T select multiple file set it to true i am seleccting it to false

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((myStream = openFileDialog1.OpenFile()) != null)
                    {
                        using (StreamReader reader = new StreamReader(myStream))
                        {
                            // Read the first line from the file and write it the textbox.
                            original = reader.ReadLine();
                        }

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }

            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            using (TripleDESCryptoServiceProvider tripledes = new TripleDESCryptoServiceProvider())
            {
                Byte[] encrypted = File.ReadAllBytes("encrypted");
                Byte[] key = File.ReadAllBytes("key");
                Byte[] tripledesIV = File.ReadAllBytes("tripledesIV");
                string roundtrip = DecryptBytesToString(encrypted, key, tripledesIV);
                MessageBox.Show("Original " + original + " Round trip " + roundtrip);
            }
        }
    }
}
