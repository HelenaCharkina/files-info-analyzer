using System;
using System.Data.SqlClient;
using System.IO;


namespace обход_файлов
{
    class Program
    {
       
        static void Main(string[] args)
        {
            Console.WriteLine("Введите абсолютный путь к базе данных:");
            string connStr = Console.ReadLine();
            string connectionString = @"Data Source = (LocalDB)\MSSQLLocalDB; AttachDbFilename =" + connStr + "; Integrated Security = True";
          
            SqlConnection db = new SqlConnection(connectionString);
           
            Console.WriteLine("Введите режим работы: 1 - записать информацию о файлах, 2 - прочитать информацию");
            string mode = Console.ReadLine();
            
            db.Open();

            string sqlExpression = "create table #tmp_table (id int, name nvarchar(50), create_date nvarchar(50), last_edit_date nvarchar(50), hash_sum nvarchar(50), " +
                "path nvarchar(100), size bigint, init_date nvarchar(50))";
            SqlCommand command = new SqlCommand(sqlExpression, db);
            command.ExecuteNonQuery();

            try
            {
                switch (mode) {
                    case "1":
                        WriteFilesInfo(db, InsertFileInfoIntoDb);
                        break;
                    case "2":
                        
                        WriteFilesInfo(db, GetFilesInfo);
                        Console.WriteLine("------------АНАЛИЗ ЗАВЕРШЕН------------");
                        Console.WriteLine("Вы хотите удалить из базы данных информацию об удаленных файлах? Если да, введите yes, иначе введите no");
                        string ans = Console.ReadLine();
                        switch (ans)
                        {
                            case "yes":
                                ClearDb(db);
                                break;
                            default:
                                break;
                        }

                        break;
                    default:
                        Console.WriteLine("Неверное значение режима работы.");
                        break;
                }
               
            }
            catch(Exception e)
            {
                Console.WriteLine("При выполнении программы произошла ошибка: {0}", e.Message);
            }
           
            db.Close();
            Console.WriteLine("Работа программы завершена.");

        }

        private static void ClearDb(SqlConnection db)
        {
            string sqlExpression = String.Format("delete from FileInfo where Id in(select id from #tmp_table)");
            SqlCommand command = new SqlCommand(sqlExpression, db);
            command.ExecuteNonQuery();
        }

        // WriteFilesInfo() функция обхода директории
        private static void WriteFilesInfo(SqlConnection db, Func<FileInfo, SqlConnection, int> func)
        {
            
            Console.WriteLine("Введите путь к директории: ");
            string path = Console.ReadLine();

            if (func == GetFilesInfo)
            {
                string likeStr = "%" + path + "%";
                string sqlExpression = String.Format("insert into #tmp_table (id, name, create_date, last_edit_date, hash_sum, path, size, init_date)" +
                    "(select Id, name, create_date, last_edit_date, hash_sum, path, size, init_date from FileInfo where path like '{0}')", likeStr);
                SqlCommand command = new SqlCommand(sqlExpression, db);
                command.ExecuteNonQuery();
            }

            Console.WriteLine("------------АНАЛИЗ ЗАПУЩЕН------------");
            DirectoryInfo dir = new DirectoryInfo(path);
            DirectoryInfo[] subDirs = dir.GetDirectories("*.*", SearchOption.AllDirectories);

            foreach (var file in dir.GetFiles())
            {
                func(file, db);
            }
            foreach (DirectoryInfo subDir in subDirs)
            {
                foreach (var file in subDir.GetFiles())
                {
                    func(file, db);
                }
            }
            if (func == GetFilesInfo)
            {
                string sqlExpression = "select name, create_date, last_edit_date, hash_sum, path, size, id from #tmp_table";
                SqlCommand command = new SqlCommand(sqlExpression, db);
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    Console.WriteLine("Файл {0} был удален!", reader[4]);

                }
                reader.Close();
            }
        }
        // insertFileInfoIntoDb() функция вставки записи в бд
        private static int InsertFileInfoIntoDb(FileInfo file, SqlConnection db)
        {

            string sqlExpression = String.Format("select count(Id) from FileInfo where path = '{0}'", file.FullName);
            SqlCommand command = new SqlCommand(sqlExpression, db);
           
            int cnt = (Int32)command.ExecuteScalar();
            if (cnt > 0)
            {
                
                sqlExpression = String.Format("update FileInfo set name = '{0}', create_date = '{1}', last_edit_date = '{2}', hash_sum = '{3}', path = '{4}'," +
                    " size = {5}, init_date = '{6}' where path = '{4}'",
                      file.Name, file.CreationTime, file.LastWriteTime, file.GetHashCode(), file.FullName, file.Length, DateTime.Now.ToString());
                command = new SqlCommand(sqlExpression, db);
                command.ExecuteNonQuery();
            } else
            {
               
                sqlExpression = String.Format("insert into FileInfo(name, create_date, last_edit_date, hash_sum, path, size, init_date) " +
                      "VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', {5}, '{6}')",
                      file.Name, file.CreationTime, file.LastWriteTime, file.GetHashCode(), file.FullName, file.Length, DateTime.Now.ToString());
                command = new SqlCommand(sqlExpression, db);
                command.ExecuteNonQuery();
            }
            return 0;
        }

        // getFilesInfo() функция получения информации об изменении файлов
        private static int GetFilesInfo(FileInfo file, SqlConnection db)
        {

            string sqlExpression = String.Format("select name, create_date, last_edit_date, hash_sum, path, size, Id from FileInfo " +
                "where path = '{0}'", file.FullName);
            SqlCommand command = new SqlCommand(sqlExpression, db);
            SqlDataReader reader = command.ExecuteReader();
            if (!reader.HasRows)
            {
                Console.WriteLine("Найден новый файл: {0}", file.FullName);
                reader.Close();
                return 0;
            }
            string findId = "";
            
            while (reader.Read())
                {
                    findId = reader[6].ToString();
                    if (file.LastWriteTime.ToString() != reader[2].ToString())
                    {
                        Console.WriteLine("Файл: {2}. Изменено время последнего редактирования. Сохраненное время: {0}, полученное время: {1}", reader[2], file.LastWriteTime, file.FullName);

                    }
                    if (file.GetHashCode().ToString() != reader[3].ToString())
                    {
                        Console.WriteLine("Файл: {2}. Изменена хэш-сумма файла. Сохраненное значение: {0}, полученное значение: {1}", reader[3], file.GetHashCode(), file.FullName);

                    }
                    if (file.Length.ToString() != reader[5].ToString())
                    {
                        Console.WriteLine("Файл: {2}. Изменен размер файла. Сохраненное значение: {0}, полученное значение: {1}", reader[5], file.Length, file.FullName);

                    }

                }
            
            reader.Close();
            if (findId != "")
            {
                sqlExpression = String.Format("delete from #tmp_table where id = {0}", findId);
                command = new SqlCommand(sqlExpression, db);
                command.ExecuteNonQuery();
            }
            return 0;
        }
    }
}
