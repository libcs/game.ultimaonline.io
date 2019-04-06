using System;
using System.Threading;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Data.Common;

namespace UltimaOnline.Engines.MyRunUO
{
    public class DatabaseCommandQueue
    {
        Queue _Queue;
        ManualResetEvent _Sync;
        Thread _Thread;
        string _CompletionString;
        string _ConnectionString;

        public bool HasCompleted { get; private set; }

        public void Enqueue(object obj)
        {
            lock (_Queue.SyncRoot)
            {
                _Queue.Enqueue(obj);
                try { _Sync.Set(); }
                catch { }
            }
        }

        public DatabaseCommandQueue(string completionString, string threadName)
            : this(Config.CompileConnectionString(), completionString, threadName) { }
        public DatabaseCommandQueue(string connectionString, string completionString, string threadName)
        {
            _CompletionString = completionString;
            _ConnectionString = connectionString;
            _Queue = Queue.Synchronized(new Queue());
            _Queue.Enqueue(null); // signal connect
            /*_Queue.Enqueue( "DELETE FROM myrunuo_characters" );
			_Queue.Enqueue( "DELETE FROM myrunuo_characters_layers" );
			_Queue.Enqueue( "DELETE FROM myrunuo_characters_skills" );
			_Queue.Enqueue( "DELETE FROM myrunuo_guilds" );
			_Queue.Enqueue( "DELETE FROM myrunuo_guilds_wars" );*/
            _Sync = new ManualResetEvent(true);
            _Thread = new Thread(new ThreadStart(Thread_Start))
            {
                Name = threadName,//"MyRunUO Database Command Queue";
                Priority = Config.DatabaseThreadPriority
            };
            _Thread.Start();
        }

        void Thread_Start()
        {
            var connected = false;
            DbConnection connection = null;
            DbCommand command = null;
            DbTransaction transact = null;
            var start = DateTime.UtcNow;
            var shouldWriteException = true;
            while (true)
            {
                _Sync.WaitOne();
                while (_Queue.Count > 0)
                {
                    try
                    {
                        var obj = _Queue.Dequeue();
                        if (obj == null)
                        {
                            if (connected)
                            {
                                if (transact != null)
                                {
                                    try { transact.Commit(); }
                                    catch (Exception commitException)
                                    {
                                        Console.WriteLine("MyRunUO: Exception caught when committing transaction");
                                        Console.WriteLine(commitException);
                                        try
                                        {
                                            transact.Rollback();
                                            Console.WriteLine("MyRunUO: Transaction has been rolled back");
                                        }
                                        catch (Exception rollbackException)
                                        {
                                            Console.WriteLine("MyRunUO: Exception caught when rolling back transaction");
                                            Console.WriteLine(rollbackException);
                                        }
                                    }
                                }
                                try { connection.Close(); }
                                catch { }
                                try { connection.Dispose(); }
                                catch { }
                                try { command.Dispose(); }
                                catch { }
                                try { _Sync.Close(); }
                                catch { }
                                Console.WriteLine(_CompletionString, (DateTime.UtcNow - start).TotalSeconds);
                                HasCompleted = true;
                                return;
                            }
                            else
                            {
                                try
                                {
                                    connected = true;
                                    connection = new SqlConnection(_ConnectionString);
                                    connection.Open();
                                    command = connection.CreateCommand();
                                    if (Config.UseTransactions)
                                    {
                                        transact = connection.BeginTransaction();
                                        command.Transaction = transact;
                                    }
                                }
                                catch (Exception e)
                                {
                                    try { if (transact != null) transact.Rollback(); }
                                    catch { }
                                    try { if (connection != null) connection.Close(); }
                                    catch { }
                                    try { if (connection != null) connection.Dispose(); }
                                    catch { }
                                    try { if (command != null) command.Dispose(); }
                                    catch { }
                                    try { _Sync.Close(); }
                                    catch { }
                                    Console.WriteLine("MyRunUO: Unable to connect to the database");
                                    Console.WriteLine(e);
                                    HasCompleted = true;
                                    return;
                                }
                            }
                        }
                        else if (obj is string)
                        {
                            command.CommandText = (string)obj;
                            command.ExecuteNonQuery();
                        }
                        else
                        {
                            var parms = (string[])obj;
                            command.CommandText = parms[0];
                            if (command.ExecuteScalar() == null)
                            {
                                command.CommandText = parms[1];
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (shouldWriteException)
                        {
                            Console.WriteLine("MyRunUO: Exception caught in database thread");
                            Console.WriteLine(e);
                            shouldWriteException = false;
                        }
                    }
                }
                lock (_Queue.SyncRoot)
                {
                    if (_Queue.Count == 0)
                        _Sync.Reset();
                }
            }
        }
    }
}