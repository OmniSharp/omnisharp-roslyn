﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Utility methods for classifying and handling exceptions.</summary>
//-----------------------------------------------------------------------

#if BUILDINGAPPXTASKS
namespace Microsoft.Build.AppxPackage.Shared
#else
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Xml;
#if FEATURE_VARIOUS_EXCEPTIONS
using System.Xml.Schema;
using System.Runtime.Serialization;
#endif

namespace Microsoft.Build.Shared
#endif
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    internal static class ExceptionHandling
    {
#if !BUILDINGAPPXTASKS
        /// <summary>
        /// The filename that exceptions will be dumped to
        /// </summary>
        private static string s_dumpFileName;
#endif
        /// <summary>
        /// If the given exception is "ignorable under some circumstances" return false.
        /// Otherwise it's "really bad", and return true.
        /// This makes it possible to catch(Exception ex) without catching disasters.
        /// </summary>
        /// <param name="e"> The exception to check. </param>
        /// <returns> True if exception is critical. </returns>
        internal static bool IsCriticalException(Exception e)
        {
            if (e is OutOfMemoryException
#if FEATURE_VARIOUS_EXCEPTIONS
             || e is StackOverflowException
             || e is ThreadAbortException
             || e is ThreadInterruptedException
             || e is AccessViolationException
#endif
#if !BUILDINGAPPXTASKS
             || e is InternalErrorException
#endif
             )
            {
                // Ideally we would include NullReferenceException, because it should only ever be thrown by CLR (use ArgumentNullException for arguments)
                // but we should handle it if tasks and loggers throw it.

                // ExecutionEngineException has been deprecated by the CLR
                return true;
            }

#if !CLR2COMPATIBILITY            
            // Check if any critical exceptions
            var aggregateException = e as AggregateException;

            if (aggregateException != null)
            {
                // If the aggregate exception contains a critical exception it is considered a critical exception
                if (aggregateException.InnerExceptions.Any(innerException => IsCriticalException(innerException)))
                {
                    return true;
                }
            }
#endif

            return false;
        }

        /// <summary>
        /// If the given exception is file IO related or expected return false.
        /// Otherwise, return true.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns>True if exception is not IO related or expected otherwise false.</returns>
        internal static bool NotExpectedException(Exception e)
        {
            return !IsIoRelatedException(e);
        }

        /// <summary>
        /// Determine whether the exception is file-IO related.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns>True if exception is IO related.</returns>
        internal static bool IsIoRelatedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }

        /// <summary> Checks if the exception is an XML one. </summary>
        /// <param name="e"> Exception to check. </param>
        /// <returns> True if exception is related to XML parsing. </returns>
        internal static bool IsXmlException(Exception e)
        {
            return e is XmlException
#if FEATURE_VARIOUS_EXCEPTIONS
                || e is XmlSyntaxException
                || e is XmlSchemaException
#endif
                || e is UriFormatException; // XmlTextReader for example uses this under the covers
        }

        /// <summary> Extracts line and column numbers from the exception if it is XML-related one. </summary>
        /// <param name="e"> XML-related exception. </param>
        /// <returns> Line and column numbers if available, (0,0) if not. </returns>
        /// <remarks> This function works around the fact that XmlException and XmlSchemaException are not directly related. </remarks>
        internal static LineAndColumn GetXmlLineAndColumn(Exception e)
        {
            var line = 0;
            var column = 0;

            var xmlException = e as XmlException;
            if (xmlException != null)
            {
                line = xmlException.LineNumber;
                column = xmlException.LinePosition;
            }
            else
            {
#if FEATURE_VARIOUS_EXCEPTIONS
                var schemaException = e as XmlSchemaException;
                if (schemaException != null)
                {
                    line = schemaException.LineNumber;
                    column = schemaException.LinePosition;
                }
#endif
            }

            return new LineAndColumn
            {
                Line = line,
                Column = column
            };
        }

#if !BUILDINGAPPXTASKS

        /// <summary>
        /// If the given exception is file IO related or Xml related return false.
        /// Otherwise, return true.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        internal static bool NotExpectedIoOrXmlException(Exception e)
        {
            if
            (
                IsXmlException(e)
                || !NotExpectedException(e)
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// If the given exception is reflection-related return false.
        /// Otherwise, return true.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        internal static bool NotExpectedReflectionException(Exception e)
        {
            // We are explicitly not handling TargetInvocationException. Those are just wrappers around
            // exceptions thrown by the called code (such as a task or logger) which callers will typically
            // want to treat differently.
            if
            (
                e is TypeLoadException                  // thrown when the common language runtime cannot find the assembly, the type within the assembly, or cannot load the type
                || e is MethodAccessException           // thrown when a class member is not found or access to the member is not permitted
                || e is MissingMethodException          // thrown when code in a dependent assembly attempts to access a missing method in an assembly that was modified
                || e is MemberAccessException           // thrown when a class member is not found or access to the member is not permitted
                || e is BadImageFormatException         // thrown when the file image of a DLL or an executable program is invalid
                || e is ReflectionTypeLoadException     // thrown by the Module.GetTypes method if any of the classes in a module cannot be loaded
                || e is TargetParameterCountException   // thrown when the number of parameters for an invocation does not match the number expected
                || e is InvalidCastException
                || e is AmbiguousMatchException         // thrown when binding to a member results in more than one member matching the binding criteria
#if FEATURE_VARIOUS_EXCEPTIONS
                || e is CustomAttributeFormatException  // thrown if a custom attribute on a data type is formatted incorrectly
                || e is InvalidFilterCriteriaException  // thrown in FindMembers when the filter criteria is not valid for the type of filter you are using
                || e is TargetException                 // thrown when an attempt is made to invoke a non-static method on a null object.  This may occur because the caller does not 
                                                        //     have access to the member, or because the target does not define the member, and so on.
#endif
                || e is MissingFieldException           // thrown when code in a dependent assembly attempts to access a missing field in an assembly that was modified.
                || !NotExpectedException(e)             // Reflection can throw IO exceptions if the assembly cannot be opened

            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Serialization has been observed to throw TypeLoadException as
        /// well as SerializationException and IO exceptions. (Obviously
        /// it has to do reflection but it ought to be wrapping the exceptions.)
        /// </summary>
        internal static bool NotExpectedSerializationException(Exception e)
        {
            if
            (
#if FEATURE_VARIOUS_EXCEPTIONS
                e is SerializationException ||
#endif
                !NotExpectedReflectionException(e)
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns false if this is a known exception thrown by the registry API.
        /// </summary>
        internal static bool NotExpectedRegistryException(Exception e)
        {
            if (e is SecurityException
             || e is UnauthorizedAccessException
             || e is IOException
             || e is ObjectDisposedException
             || e is ArgumentException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns false if this is a known exception thrown by function evaluation
        /// </summary>
        internal static bool NotExpectedFunctionException(Exception e)
        {
            if (e is InvalidCastException
             || e is ArgumentNullException
             || e is FormatException
             || e is InvalidOperationException
             || !NotExpectedReflectionException(e))
            {
                return false;
            }

            return true;
        }

#if FEATURE_APPDOMAIN_UNHANDLED_EXCEPTION
        /// <summary>
        /// Dump any unhandled exceptions to a file so they can be diagnosed
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "It is called by the CLR")]
        internal static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            DumpExceptionToFile(ex);
        }
#endif

        /// <summary>
        /// Dump the exception information to a file
        /// </summary>
        internal static void DumpExceptionToFile(Exception ex)
        {
            //  Locking on a type is not recommended.  However, we are doing it here to be extra cautious about compatibility because 
            //  this method previously had a [MethodImpl(MethodImplOptions.Synchronized)] attribute, which does lock on the type when 
            //  applied to a static method.
            lock (typeof(ExceptionHandling))
            {
                if (s_dumpFileName == null)
                {
                    Guid guid = Guid.NewGuid();
                    string tempPath = Path.GetTempPath();

                    // For some reason we get Watson buckets because GetTempPath gives us a folder here that doesn't exist.
                    // Either because %TMP% is misdefined, or because they deleted the temp folder during the build.
                    if (!Directory.Exists(tempPath))
                    {
                        // If this throws, no sense catching it, we can't log it now, and we're here
                        // because we're a child node with no console to log to, so die
                        Directory.CreateDirectory(tempPath);
                    }

                    s_dumpFileName = Path.Combine(tempPath, "MSBuild_" + guid.ToString() + ".failure.txt");

                    using (StreamWriter writer = FileUtilities.OpenWrite(s_dumpFileName, append: true))
                    {
                        writer.WriteLine("UNHANDLED EXCEPTIONS FROM PROCESS {0}:", Process.GetCurrentProcess().Id);
                        writer.WriteLine("=====================");
                    }
                }

                using (StreamWriter writer = FileUtilities.OpenWrite(s_dumpFileName, append: true))
                {
                    // "G" format is, e.g., 6/15/2008 9:15:07 PM
                    writer.WriteLine(DateTime.Now.ToString("G", CultureInfo.CurrentCulture));
                    writer.WriteLine(ex.ToString());
                    writer.WriteLine("===================");
                }
            }
        }
#endif

        /// <summary> Line and column pair. </summary>
        internal struct LineAndColumn
        {
            /// <summary> Gets or sets line number. </summary>
            internal int Line { get; set; }

            /// <summary> Gets or sets column position. </summary>
            internal int Column { get; set; }
        }
    }
}
