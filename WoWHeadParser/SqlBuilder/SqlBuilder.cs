﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoWHeadParser.Properties;

namespace Sql
{
    public enum SqlQueryType : byte
    {
        None,
        Update,
        Replace,
        Insert,
        InsertIgnore,
        Max,
    }

    public class SqlBuilder
    {
        /// <summary>
        /// Gets a sql query type
        /// </summary>
        public SqlQueryType QueryType { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to allow null values
        /// </summary>
        public bool AllowNullValue { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to allow append delete query
        /// </summary>
        public bool AppendDeleteQuery { get; private set; }

        private string _tableName = string.Empty;

        private string _keyName = string.Empty;

        private List<string> _fields = new List<string>(64);

        private List<SqlItem> _items = new List<SqlItem>(64);

        private StringBuilder _content = new StringBuilder(8196);

        /// <summary>
        /// Initial Sql builder
        /// </summary>
        /// <param name="tableName">Table name (like creature_template, creature etc.)</param>
        /// <param name="keyName">Key name (like entry, id, guid etc.)</param>
        public SqlBuilder(string tableName, string keyName)
        {
            _tableName = tableName;
            _keyName = keyName;

            AppendDeleteQuery = Settings.Default.AppendDeleteQuery;
            AllowNullValue = Settings.Default.AllowEmptyValues;
            QueryType = (SqlQueryType)Settings.Default.QueryType;

            if (QueryType <= SqlQueryType.None || QueryType >= SqlQueryType.Max)
                throw new InvalidQueryTypeException(QueryType);
        }

        /// <summary>
        /// Initial Sql builder
        /// <param name="tableName">Table name (like creature_template, creature etc.)</param>
        /// </summary>
        public SqlBuilder(string tableName)
                : this(tableName, "entry")
        {
        }

        /// <summary>
        /// Append fields name
        /// </summary>
        /// <param name="args">fields name array</param>
        public void SetFieldsName(params string[] args)
        {
            if (args == null)
                throw new ArgumentNullException();

            for (int i = 0; i < args.Length; ++i)
            {
                _fields.Add(args[i]);
            }
        }

        /// <summary>
        /// Append key and fields value 
        /// </summary>
        /// <param name="key">key value</param>
        /// <param name="args">string fields values array</param>
        public void AppendFieldsValue(object key, params string[] args)
        {
            if (key == null || args == null)
                throw new ArgumentNullException();

            List<string> values = new List<string>(args.Length);
            for (int i = 0; i < args.Length; ++i)
            {
                values.Add(args[i]);
            }

            _items.Add(new SqlItem(key, values));
        }

        /// <summary>
        /// Append key and fields value 
        /// </summary>
        /// <param name="key">key value</param>
        /// <param name="args">object fields values array</param>
        public void AppendFieldsValue(object key, params object[] args)
        {
            if (key == null || args == null)
                throw new ArgumentNullException();

            List<string> values = new List<string>(args.Length);
            for (int i = 0; i < args.Length; ++i)
            {
                values.Add(args[i].ToString());
            }

            _items.Add(new SqlItem(key, values));
        }

        /// <summary>
        /// Append sql query
        /// </summary>
        /// <param name="query"></param>
        public void AppendSqlQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException();

            _content.AppendLine(query);
        }

        public bool Empty
        {
            get { return _items.Count <= 0; }
        }

        /// <summary>
        /// Build sql query
        /// </summary>
        public override string ToString()
        {
            if (Empty)
                return string.Empty;

            _content.Capacity = 2048 * _items.Count;

            switch (QueryType)
            {
                case SqlQueryType.Update:
                    return BuildUpdateQuery();
                case SqlQueryType.Replace:
                case SqlQueryType.Insert:
                case SqlQueryType.InsertIgnore:
                    return BuildReplaceInsertQuery();
                default:
                    return string.Empty;
            }
        }

        private string BuildUpdateQuery()
        {
            for (int i = 0; i < _items.Count; ++i)
            {
                bool notEmpty = false;

                SqlItem item = _items[i];

                StringBuilder contentInternal = new StringBuilder(1024);
                {
                    contentInternal.AppendFormat("UPDATE `{0}` SET ", _tableName);
                    for (int j = 0; j < item.Count; ++j)
                    {
                        if (!AllowNullValue && string.IsNullOrWhiteSpace(item[j]))
                            continue;

                        contentInternal.AppendFormat(NumberFormatInfo.InvariantInfo, "`{0}` = '{1}', ", _fields[j], item[j]);
                        notEmpty = true;
                    }
                    contentInternal.Remove(contentInternal.Length - 2, 2);
                    contentInternal.AppendFormat(" WHERE `{0}` = {1};", _keyName, item.Key).AppendLine();

                    if (notEmpty)
                        _content.Append(contentInternal.ToString());
                }
            }

            return _content.ToString();
        }

        private string BuildReplaceInsertQuery()
        {
            if (AppendDeleteQuery)
                _content.AppendFormat("DELETE FROM `{0}` WHERE `{1}` = '{2}';", _tableName, _keyName, _items[0].Key).AppendLine();

            switch (QueryType)
            {
                case SqlQueryType.Insert:
                    _content.AppendFormat("INSERT INTO `{0}` (`{1}`, ", _tableName, _keyName);
                    break;
                case SqlQueryType.InsertIgnore:
                    _content.AppendFormat("INSERT IGNORE INTO `{0}` (`{1}`, ", _tableName, _keyName);
                    break;
                case SqlQueryType.Replace:
                    _content.AppendFormat("REPLACE INTO `{0}` (`{1}`, ", _tableName, _keyName);
                    break;
            }

            for (int i = 0; i < _fields.Count; ++i)
                _content.AppendFormat("`{0}`, ", _fields[i]);

            _content.Remove(_content.Length - 2, 2);
            _content.AppendLine(") VALUES");

            for (int i = 0; i < _items.Count; ++i)
            {
                SqlItem item = _items[i];

                _content.AppendFormat("('{0}', ", item.Key);
                for (int j = 0; j < item.Count; ++j)
                {
                    _content.AppendFormat(NumberFormatInfo.InvariantInfo, "'{0}', ", item[j]);
                }
                _content.Remove(_content.Length - 2, 2);
                _content.AppendFormat("){0}", i < _items.Count - 1 ? "," : ";").AppendLine();
            }

            return _content + Environment.NewLine;
        }
    }
}