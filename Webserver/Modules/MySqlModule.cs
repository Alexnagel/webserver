﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace Webserver.Modules
{
    class MySqlModule
    {
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
            //Insert();
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

        public void Insert()
        {
            string query = @"INSERT INTO user (id, firstname, lastname) VALUES('1,','Stefan', 'van der Pas')";

            //open connection
            if (this.OpenConnection() == true)
            {
                //create command and assign the query and connection from the constructor
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //Execute command
                cmd.ExecuteNonQuery();

                //close connection
                this.CloseConnection();
            }
        }

        public Dictionary<bool,string> CheckUser(String username, String password)
        {
            Dictionary<bool, string> loginCred = new Dictionary<bool, string>();
            string query = "SELECT rights FROM user WHERE username=@User AND password=@Pass;";
            if(this.OpenConnection() == true)
            {
                MySqlCommand cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@User", username);
                cmd.Parameters.AddWithValue("@Pass", password);
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
    }
}
