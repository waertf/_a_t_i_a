using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Devart.Data.PostgreSql;
using System.Data;
using log4net;
using log4net.Config;

namespace ATIA_2
{
    class SqlClient : IDisposable
    {
        PgSqlConnectionStringBuilder pgCSB = new PgSqlConnectionStringBuilder();
        PgSqlConnectionStringBuilder pgCSB2 = new PgSqlConnectionStringBuilder();
        PgSqlConnection pgSqlConnection2;
        PgSqlConnection pgSqlConnection;
        public bool IsConnected { get; set; }
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public SqlClient(string ip, string port, string user_id, string password, string database, string Pooling, string MinPoolSize, string MaxPoolSize, string ConnectionLifetime)
        {
            pgCSB.Host = ip;
            pgCSB.Port = pgCSB2.Port = int.Parse(port);
            pgCSB.UserId = pgCSB2.UserId = user_id;
            pgCSB.Password = pgCSB2.Password = password;
            pgCSB.Database = pgCSB2.Database = database;


            pgCSB.Pooling = bool.Parse(Pooling);
            pgCSB.MinPoolSize = int.Parse(MinPoolSize);
            pgCSB.MaxPoolSize = int.Parse(MaxPoolSize);
            pgCSB.ConnectionLifetime = int.Parse(ConnectionLifetime); ;

            pgCSB.Unicode = true;
            pgSqlConnection = new PgSqlConnection(pgCSB.ConnectionString);

            pgCSB2.Host = ConfigurationManager.AppSettings["DB2_ADDRESS"];
            pgCSB2.Unicode = true;
        }
        public bool connect()
        {
            try
            {
                if (pgSqlConnection != null)
                {
                    pgSqlConnection.Open();
                    IsConnected = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (PgSqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Connect exception occurs: {0}", ex.Error);
                log.Error("Connect exception occurs: " + ex.Error);
                Console.ResetColor();
                return false;
            }
        }
        public bool disconnect()
        {
            try
            {
                if (pgSqlConnection != null)
                {
                    pgSqlConnection.Close();
                    IsConnected = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (PgSqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Disconnect exception occurs: {0}", ex.Error);
                log.Error("Disconnect exception occurs: " + ex.Error);
                Console.ResetColor();
                return false;
            }
        }
        //For UPDATE, INSERT, and DELETE statements
        public bool modify(string cmd)
        {
            System.Threading.Thread accessDb2Thread = new System.Threading.Thread
      (delegate()
      {
          modifyDB2(cmd);
      });
            accessDb2Thread.Start();
            try
            {
                if (pgSqlConnection != null && IsConnected)
                {
                    //insert
                    PgSqlCommand command = pgSqlConnection.CreateCommand();
                    command.CommandText = cmd;
                    //cmd.CommandText = "INSERT INTO public.test (id) VALUES (1)";
                    pgSqlConnection.BeginTransaction();
                    //async
                    IAsyncResult cres = command.BeginExecuteNonQuery(null, null);
                    /*
                    Console.Write("In progress...");
                    while (!cres.IsCompleted)
                    {
                        Console.Write(".");
                        //Perform here any operation you need
                    }
                    */
                    /*
                    if (cres.IsCompleted)
                        Console.WriteLine("Completed.");
                    else
                        Console.WriteLine("Have to wait for operation to complete...");
                    */
                    int RowsAffected = command.EndExecuteNonQuery(cres);
                    //Console.WriteLine("Done. Rows affected: " + RowsAffected.ToString());
                    /*
                     //sync
                     int aff = cmd.ExecuteNonQuery();
                     Console.WriteLine(aff + " rows were affected.");
                     * 
                     */
                    pgSqlConnection.Commit();
                    accessDb2Thread.Join();
                    return true;
                }
                else
                {
                    accessDb2Thread.Join();
                    return false;
                }
            }
            catch (PgSqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Modify exception occurs: {0}" + Environment.NewLine + "{1}", ex.Error, cmd);
                log.Error("Modify exception occurs: " + Environment.NewLine + ex.Error + Environment.NewLine + cmd);
                Console.ResetColor();
                pgSqlConnection.Rollback();
                accessDb2Thread.Join();
                return false;
            }

        }
        //For SELECT statements
        public DataTable get_DataTable(string cmd)
        {
            try
            {
                if (pgSqlConnection != null && IsConnected)
                {
                    DataTable datatable = new DataTable();
                    PgSqlCommand command = pgSqlConnection.CreateCommand();
                    command.CommandText = cmd;
                    //Console.WriteLine("Starting asynchronous retrieval of data...");
                    IAsyncResult cres = command.BeginExecuteReader();
                    /*
                    Console.Write("In progress...");
                    while (!cres.IsCompleted)
                    {
                        Console.Write(".");
                        //Perform here any operation you need
                    }
                    */
                    //if (cres.IsCompleted)
                    //Console.WriteLine("Completed.");
                    //else
                    //Console.WriteLine("Have to wait for operation to complete...");
                    PgSqlDataReader myReader = command.EndExecuteReader(cres);
                    try
                    {
                        // printing the column names
                        for (int i = 0; i < myReader.FieldCount; i++)
                        {
                            //Console.Write(myReader.GetName(i).ToString() + "\t");
                            datatable.Columns.Add(myReader.GetName(i).ToString(), typeof(string));
                        }
                        //Console.Write(Environment.NewLine);
                        while (myReader.Read())
                        {
                            DataRow dr = datatable.NewRow();

                            for (int i = 0; i < myReader.FieldCount; i++)
                            {
                                //Console.Write(myReader.GetString(i) + "\t");
                                dr[i] = myReader.GetString(i);
                            }
                            datatable.Rows.Add(dr);
                            //Console.Write(Environment.NewLine);
                            //Console.WriteLine(myReader.GetInt32(0) + "\t" + myReader.GetString(1) + "\t");
                        }
                    }
                    finally
                    {
                        myReader.Close();
                    }
                    /*
                    foreach (DataRow row in datatable.Rows) // Loop over the rows.
                    {
                        Console.WriteLine("--- Row ---"); // Print separator.
                        foreach (var item in row.ItemArray) // Loop over the items.
                        {
                            Console.Write("Item: "); // Print label.
                            Console.WriteLine(item); // Invokes ToString abstract method.
                        }
                    }
                    */
                    return datatable;
                }
                else
                    return null;
            }
            catch (PgSqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("GetDataTable exception occurs: {0}" + Environment.NewLine + "{1}", ex.Error, cmd);
                log.Error("GetDataTable exception occurs: " + Environment.NewLine + ex.Error + Environment.NewLine + cmd);
                Console.ResetColor();
                return null;
            }
        }

        public void Dispose()
        {
            pgSqlConnection.Dispose();
        }
        void modifyDB2(string cmd)
        {
            //Stopwatch stopWatch = new Stopwatch();
            PgSqlCommand command = null;
            PgSqlTransaction myTrans = null;
            using (pgSqlConnection2 = new PgSqlConnection(pgCSB2.ConnectionString))
                try
                {

                    //{
                        pgSqlConnection2.Open();
                        //insert
                        command = pgSqlConnection2.CreateCommand();
                        command.CommandText = cmd;
                        //command.CommandTimeout = 30;

                        //cmd.CommandText = "INSERT INTO public.test (id) VALUES (1)";
                        //pgSqlConnection.BeginTransaction();
                        //async
                        int RowsAffected;


                        //lock (accessLock)
                        //{
                            myTrans = pgSqlConnection2.BeginTransaction(IsolationLevel.ReadCommitted);
                            command.Transaction = myTrans;
                            //IAsyncResult cres = command.BeginExecuteNonQuery();
                            //RowsAffected = command.EndExecuteNonQuery(cres);
                            //lock (accessLock)
                            RowsAffected = command.ExecuteNonQuery();
                            myTrans.Commit();
                        //}
                        pgSqlConnection2.Close();
                        //IAsyncResult cres=command.BeginExecuteNonQuery(null,null);
                        //Console.Write("In progress...");
                        //while (!cres.IsCompleted)
                        //{
                            //Console.Write(".");
                            //Perform here any operation you need
                        //}
                        /*
                        if (cres.IsCompleted)
                            Console.WriteLine("Completed.");
                        else
                            Console.WriteLine("Have to wait for operation to complete...");
                        */
                        //int RowsAffected = command.EndExecuteNonQuery(cres);
                        //Console.WriteLine("Done. Rows affected: " + RowsAffected.ToString());

                        //sync
                        //int aff = command.ExecuteNonQuery();
                        //Console.WriteLine(RowsAffected + " rows were affected.");
                        //command.Dispose();
                        command = null;
                        //pgSqlConnection.Commit();
                        /*
                        ThreadPool.QueueUserWorkItem(callback =>
                        {
                        
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(RowsAffected + " rows were affected.");
                            Console.WriteLine(
                                "S++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                            Console.WriteLine("sql Write:\r\n" + cmd);
                            Console.WriteLine(
                                "E++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                            Console.ResetColor();
                            log.Info("sql Write:\r\n" + cmd);
                        });
                        */
                        //stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        //TimeSpan ts = stopWatch.Elapsed;

                        // Format and display the TimeSpan value.
                        //string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        //ts.Hours, ts.Minutes, ts.Seconds,
                        //ts.Milliseconds / 10);
                        //SiAuto.Main.AddCheckpoint(Level.Debug, "sql modify take time:" + elapsedTime, cmd);

                    //}

                }
                catch (PgSqlException ex)
                {
                    if (myTrans != null) myTrans.Rollback();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Modify exception occurs: {0}" + Environment.NewLine + "{1}", ex.Error, cmd);
                    log.Error("Modify exception occurs: " + Environment.NewLine + ex.Error + Environment.NewLine + cmd);
                    Console.ResetColor();
                    //pgSqlConnection.Rollback();
                    //command.Dispose();
                    command = null;
                    pgSqlConnection2.Close();

                }


        }
    }
}
