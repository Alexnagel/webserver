using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Security.Cryptography;

namespace Webserver.Modules
{
    class MySqlModule
    {
        private const String SALT = "y6&!sVu?TfwO:<p!^-/;(<$HrEVDFg57Gr_j-p_g[u.-`{a|@ZZNhK-i+Z+rHbv7";
        private MySqlConnection connection;
        private string server;
        private string database;
        private string uid;
        private string password;

        public MySqlModule()
        {
            Initialize();
        }

        private void Initialize()
        {
            server = "ispirato.nl";
            database = "pcwfelui_web";
            uid = "pcwfelui_web";
            password = "webserver";
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" +
            database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            connection = new MySqlConnection(connectionString);
        }

        private bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        MessageBox.Show("Cannot connect to server.  Contact administrator");
                        break;

                    case 1045:
                        MessageBox.Show("Invalid username/password, please try again");
                        break;
                }
                return false;
            }
        }

        //Close connection
        private bool CloseConnection()
        {
            try
            {
                connection.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        // Check for valid login credentials
        public Dictionary<bool,string> CheckUser(String username, String password)
        {
            
            Dictionary<bool, string> loginCred = new Dictionary<bool, string>();
            String query = "SELECT rights FROM user WHERE username=@User AND password=@Pass;";
            String sHashedPassword = createMD5Hash(password);

            if(this.OpenConnection() == true)
            {
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@User", username);
                cmd.Parameters.AddWithValue("@Pass", sHashedPassword);
                MySqlDataReader dr = cmd.ExecuteReader();
                String result = "";
                while (dr.Read())
                {
                    result = dr[0].ToString();
                }

                if(!string.IsNullOrEmpty(result))
                {
                    loginCred.Add(true, result);
                }
                else
                {
                    loginCred.Add(false, result);
                }
                this.CloseConnection();
            }

            return loginCred;
        }

        // Create a new user
        public void CreateUser(String username, String password, String rights)
        {
            string query = @"INSERT INTO user (id, username, password, rights) VALUES(@Id, @Username, @Password, @Rights)";

            //open connection
            if (this.OpenConnection() == true)
            {
                // Check rows for to get ID+1
                string idQuery = "SELECT COUNT(*)+1 FROM user";
                MySqlCommand idCmd = new MySqlCommand(idQuery, connection);
                object tempCount = idCmd.ExecuteScalar();
                int count = int.Parse(tempCount.ToString());

                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);
                
                // Create hashed password
                String sHashedPassword = createMD5Hash(password);
                
                // Add parameters to prevent sql injection
                cmd.Parameters.AddWithValue("Id", count);
                cmd.Parameters.AddWithValue("Username", username);
                cmd.Parameters.AddWithValue("Password", sHashedPassword);
                cmd.Parameters.AddWithValue("Rights", rights);

                //Execute command
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }

        // Create hash for security in password
        private String createMD5Hash(String sToHash)
        {
            // Add salt to string
            sToHash += SALT;

            MD5 hasher = MD5.Create();
            byte[] bHashedString = hasher.ComputeHash(Encoding.ASCII.GetBytes(sToHash));

            StringBuilder sHashedString = new StringBuilder();
            for(int i = 0; i < bHashedString.Length; i++)
            {
                sHashedString.Append(bHashedString[i].ToString("x2"));
            }
            return sHashedString.ToString();

        }
    }
}
