using System;
using System.IO;
using System.Reflection;
using TypeLitePlus.TsModels;

namespace TypeLitePlus
{
    /// <summary>
    /// Provides helper methods for generating TypeScript definition files.
    /// </summary>
    public static class TypeScript
    {
        public static string LogPath { get; set; }

        internal static TextWriter LogWriter { get; private set; }

        /// <summary>
        /// Creates an instance of the FluentTsModelBuider for use in T4 templates.
        /// </summary>
        /// <returns>An instance of the FluentTsModelBuider</returns>
        public static TypeScriptFluent Definitions()
        {
            return new TypeScriptFluent();
        }

        /// <summary>
        /// Creates an instance of the FluentTsModelBuider for use in T4 templates.
        /// </summary>
        /// <param name="scriptGenerator">The script generator you want it constructed with</param>
        /// <returns>An instance of the FluentTsModelBuider</returns>
        public static TypeScriptFluent Definitions(TsGenerator scriptGenerator)
        {
            return new TypeScriptFluent(scriptGenerator);
        }

        internal static void Log(string message)
        {
            if (string.IsNullOrEmpty(LogPath)
                || string.IsNullOrEmpty(message))
            {
                return;
            }
            if (LogWriter == null)
            {
                LogWriter = new StreamWriter(LogPath)
                {
                    AutoFlush = true,
                };
            }
            LogWriter.WriteLine(message);
        }

        internal static void Log2(Type typeName, string methodName, string message)
        {
            Log($"{typeName.Name}.{methodName}: {message}");
        }

        public static void CloseLog()
        {
            if (LogWriter == null)
            {
                return;
            }
            LogWriter.Close();
        }
    }
}