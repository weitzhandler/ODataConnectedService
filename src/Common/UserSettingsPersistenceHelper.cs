﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.VisualStudio.ConnectedServices;

namespace Microsoft.OData.ConnectedService.Common
{
    internal class UserSettingsPersistenceHelper
    {
        /// <summary>
        /// Saves user settings to isolated storage.  The data is stored with the user's roaming profile.
        /// </summary>
        /// <remarks>
        /// Non-critical exceptions are handled by writing an error message in the output window.
        /// </remarks>
        public static void Save(object userSettings, string providerId, string name, Action onSaved, ConnectedServiceLogger logger)
        {
            string fileName = UserSettingsPersistenceHelper.GetStorageFileName(providerId, name);

            UserSettingsPersistenceHelper.ExecuteNoncriticalOperation(
                () =>
                {
                    using (IsolatedStorageFile file = UserSettingsPersistenceHelper.GetIsolatedStorageFile())
                    {
                        IsolatedStorageFileStream stream = null;
                        try
                        {
                            // note: this overwrites existing settings file if it exists
                            stream = file.OpenFile(fileName, FileMode.Create);
                            using (XmlWriter writer = XmlWriter.Create(stream))
                            {
                                stream = null;

                                DataContractSerializer dcs = new DataContractSerializer(userSettings.GetType());
                                dcs.WriteObject(writer, userSettings);

                                writer.Flush();
                            }
                        }
                        finally
                        {
                            stream?.Dispose();
                        }
                    }

                    onSaved?.Invoke();
                },
                logger,
                "Failed loading the {0} user settings",
                fileName);
        }

        /// <summary>
        /// Loads user settings from isolated storage.
        /// </summary>
        /// <remarks>
        /// Non-critical exceptions are handled by writing an error message in the output window and 
        /// returning null.
        /// </remarks>
        public static T Load<T>(string providerId, string name, Action<T> onLoaded, ConnectedServiceLogger logger) where T : class
        {
            string fileName = UserSettingsPersistenceHelper.GetStorageFileName(providerId, name);
            T result = null;

            UserSettingsPersistenceHelper.ExecuteNoncriticalOperation(
                () =>
                {
                    using (IsolatedStorageFile file = UserSettingsPersistenceHelper.GetIsolatedStorageFile())
                    {
                        if (file.FileExists(fileName))
                        {
                            IsolatedStorageFileStream stream = null;
                            try
                            {
                                stream = file.OpenFile(fileName, FileMode.Open);
                                XmlReaderSettings settings = new XmlReaderSettings()
                                {
                                    XmlResolver = null
                                };

                                using (XmlReader reader = XmlReader.Create(stream, settings))
                                {
                                    stream = null;

                                    DataContractSerializer dcs = new DataContractSerializer(typeof(T));
                                    result = dcs.ReadObject(reader) as T;
                                }
                            }
                            finally
                            {
                                stream?.Dispose();
                            }

                            if (onLoaded != null && result != null)
                            {
                                onLoaded(result);
                            }
                        }
                    }
                },
                logger,
                "Failed loading the {0} user settings",
                fileName);

            return result;
        }

        private static string GetStorageFileName(string providerId, string name)
        {
            return providerId + "_" + name + ".xml";
        }

        private static IsolatedStorageFile GetIsolatedStorageFile()
        {
            return IsolatedStorageFile.GetStore(
                IsolatedStorageScope.Assembly | IsolatedStorageScope.User | IsolatedStorageScope.Roaming, null, null);
        }

        private static void ExecuteNoncriticalOperation(
            Action operation,
            ConnectedServiceLogger logger,
            string failureMessage,
            string failureMessageArg)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                logger.WriteMessageAsync(LoggerMessageCategory.Warning, failureMessage, failureMessageArg, ex);
            }
        }
    }
}