using System;
using System.Collections;
using System.Data;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace XMLcompare
{
    class Program
    {
        static Hashtable sharedTableNames = new Hashtable();
        static Hashtable excludedColumns = new Hashtable();

        // program entry point (with command line args)
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: XMLcompare file1 file2 [excludedColumns] [rootNodeName] [defaultNamespace]");
                return;
            }
            string fileName1 = args[0];
            string fileName2 = args[1];

            // handle custom excluded columns
            if (args.Length > 2)
            {
                ProcessExcludedColumns(args[2]);
            }

            // default values for root node name and namespace can be replaced by command line args
            string rootNodeName = args.Length > 3 ? args[3] : "root";
            string defaultNamespace = args.Length > 4 ? args[4] : "http://tempuri.org";

            try
            {
                DataSet ds1 = GetDataSet(fileName1, rootNodeName, defaultNamespace);
                DataSet ds2 = GetDataSet(fileName2, rootNodeName, defaultNamespace);

                CompareDataSets(ds1, ds2);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error: {ex.Message}");
            }
        }

        // process excluded columns from command line arg
        private static void ProcessExcludedColumns(string columnsArg)
        {
            string[] columns = columnsArg.Split(',');

            foreach (string column in columns)
            {
                if (!excludedColumns.ContainsKey(column))
                {
                    Console.WriteLine($"column {column} will be ignored in the comparison.");
                    excludedColumns.Add(column, "");
                }
            }
        }

        // read XML file and convert it into a dataset
        static DataSet GetDataSet(string fileName, string rootNode, string namespaceUri)
        {
            DataSet ds = new DataSet();
            try
            {
                XPathDocument document = new XPathDocument(fileName);
                XPathNavigator navigator = document.CreateNavigator();

                XmlNamespaceManager manager = new XmlNamespaceManager(navigator.NameTable);
                manager.AddNamespace("ns", namespaceUri);

                XPathNodeIterator nodes = navigator.Select($"/ns:{rootNode}/*", manager);

                if (nodes.Count == 0)
                {
                    throw new ApplicationException($"cannot get table nodes from file {fileName} using root node name {rootNode}");

                }

                // process tables and columns
                ProcessTablesAndColumns(nodes, ds, manager, rootNode);

                // populate tables with data
                PopulateTables(ds, manager, rootNode);

                return ds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error processing file {fileName}: {ex.Message}");
                throw;
            }
        }

        // process tables and columns from XML nodes
        private static void ProcessTablesAndColumns(XPathNodeIterator nodes, DataSet ds, XmlNamespaceManager manager, string rootNode)
        {
            while (nodes.MoveNext())
            {
                string tableName = nodes.Current.Name;
                if (!ds.Tables.Contains(tableName))
                {
                    DataTable dataTable = ds.Tables.Add(tableName);
                    AddColumnsToTable(dataTable, manager, rootNode, tableName);
                }
            }
        }

        //
        private static void AddColumnsToTable(DataTable dataTable, XmlNamespaceManager manager, string rootNode, string tableName)
        {
            XPathNodeIterator columnNodes = manager.Select($"/ns:{rootNode}/ns:{tableName}/*");
            Hashtable htColumns = new Hashtable();

            while (columnNodes.MoveNext())
            {
                string columnName = columnNodes.Current.Name;
                if (!htColumns.ContainsKey(columnName) && !excludedColumns.ContainsKey($"{tableName}.{columnName}"))
                {
                    htColumns.Add(columnName, "");
                    Console.WriteLine($"\tAdding column {columnName}");
                    dataTable.Columns.Add(columnName, typeof(String));
                }
            }
        }

        // populate dataset tables with data
        private static void PopulateTables(DataSet ds, XmlNamespaceManager manager, string rootNode)
        {
            foreach (DataTable dataTable in ds.Tables)
            {
                XPathNodeIterator nodes = navigator.Select($"/ns:{rootNode}/ns:{dataTable.TableName}", manager);

                while (nodes.MoveNext())
                {
                    DataRow dataRow = dataTable.NewRow();

                    foreach (DataColumn dataColumn in dataTable.Columns)
                    {
                        XPathNavigator nav = nodes.Current.SelectSingleNode($"ns:{dataColumn.ColumnName}", manager);

                        if (nav != null)
                        {
                            dataRow[dataColumn.ColumnName] = nav.InnerXml;
                        }
                    }

                    dataTable.Rows.Add(dataRow);
                }
            }
        }

        // compare table names in 2 datasets
        static bool CompareTableNames(DataSet ds1, DataSet ds2, out string tableNames)
        {
            bool diffFound = false;
            StringBuilder tableNamesBuilder = new StringBuilder();

            foreach (DataTable dataTable in ds1.Tables)
            {
                string tableName = dataTable.TableName;
                if (!ds2.Tables.Contains(tableName))
                {
                    tableNamesBuilder.AppendLine($"\t{tableName}");
                    diffFound = true;
                }
                else
                {
                    if (!sharedTableNames.ContainsKey(tableName))
                    {
                        sharedTableNames.Add(tableName, "");
                    }
                }
            }

            tableNames = tableNamesBuilder.ToString();
            return diffFound;
        }

        // compare column names in 2 datasets
        static bool CompareColumnNames(DataSet ds1, DataSet ds2, out string columnName)
        {
            bool diffFound = false;
            StringBuilder columnNamesBuilder = new StringBuilder();
            Hashtable diffTableNames = new Hashtable();

            foreach (string tableName in sharedTableNames.Keys)
            {
                foreach (DataColumn dataColumn in ds1.Tables[tableName].Columns)
                {
                    if (!ds2.Tables[tableName].Columns.Contains(dataColumn.ColumnName))
                    {
                        columnNamesBuilder.AppendLine($"\t{tableName}.{dataColumn.ColumnName}");
                        if (!diffTableNames.ContainsKey(tableName))
                        {
                            diffTableNames.Add(tableName, "");
                        }

                        diffFound = true;
                    }
                }
            }

            foreach (string tableName in diffTableNames.Keys)
            {
                sharedTableNames.Remove(tableName);
            }

            columnNames = columnNamesBuilder.ToString();
            return diffFound;
        }

        // compare rows in shared tables of 2 datasets
        static bool CompareRows(DataSet ds1, DataSet ds2, out string differentRows)
        {
            bool diffFound = false;
            StringBuilder diffRowsBuilder = new StringBuilder();

            foreach (string tableName in sharedTableNames.Keys)
            {
                Console.WriteLine($"comparing data for table {tableName}");

                foreach (DataRow dataRow in ds1.Tables[tableName].Rows)
                {
                    StringBuilder filterBuilder = new StringBuilder();
                    foreach (DataColumn dataColumn in ds1.Tables[tableName].Columns)
                    {
                        string columnValue = dataRow[dataColumn.ColumnName].ToString().Replace("'", "''");
                        if (!string.IsNullOrEmpty(columnValue))
                        {
                            filterBuilder.AppendLine($" AND {dataColumn.ColumnName}='{columnValue}'");

                        }
                    }

                    string filter = filterBuilder.ToString().TrimStart(" AND ".ToCharArray());

                    if (ds2.Tables[tableName].Select(filter).Length == 0)
                    {
                        diffRowsBuilder.AppendLine($"<table name=\"{tableName}\">{filter}</table>");
                        diffFound = true;
                    }
                }
            }

            differentRows = diffRowsBuilder.ToString();
            return diffFound;
        }

        // compare 2 datasets
        static void CompareDataSets(DataSet ds1, DataSet ds2)
        {
            if (CompareTableNames(ds1, ds2, out string leftTables))
            {
                Console.WriteLine("tables in left file only:");
                Console.WriteLine(leftTables);
            }

            if (CompareTableNames(ds2, ds1, out string rightTables))
            {
                Console.WriteLine("tables in right file only:");
                Console.WriteLine(rightTables);

            }

            if (CompareColumnNames(ds1, ds2, out string leftColumns))
            {
                Console.WriteLine("columns in left file only:");
                Console.WriteLine(leftColumns);
            }

            if (CompareColumnNames(ds2, ds1, out string rightColumns))
            {
                Console.WriteLine("columns in right file only:");
                Console.WriteLine(rightColumns);
            }

            if (CompareRows(ds1, ds2, out string leftRows))
            {
                SaveDifferencesToFile(leftRows, "rowsLeft.xml");
                Console.WriteLine("rows in left file only: saved to file rowsLeft.xml");
            }

            if (CompareRows(ds2, ds1, out string rightRows))
            {
                SaveDifferencesToFile(rightRows, "rowsRight.xml");
                Console.WriteLine("rows in right file only: saved to file rowsRight.xml");
            }
        }

        // save differences to XML
        static void SaveDifferencesToFile(string content, string filename)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml($"<root>{content}</root>");
            xmlDoc.Save(filename);
        }
    }
}