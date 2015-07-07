﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure.Management.Automation;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace AutomationISE.Model
{
    public class LocalAssetsStore
    {
        public static void Set(String workspacePath, ICollection<AutomationAsset> newAssets)
        {
            LocalAssets localAssets = LocalAssetsStore.Get(workspacePath);

            // add / update variables
            foreach (var newAsset in newAssets)
            {
                if (!(newAsset is AutomationVariable))
                {
                    continue;
                }
                
                bool found = false;
                foreach (var currentLocalAsset in localAssets.Variables)
                {
                    if (newAsset.Name == currentLocalAsset.Name)
                    {
                        found = true;
                        currentLocalAsset.Update(newAsset);
                    }
                }

                if (!found)
                {
                    localAssets.Variables.Add(new VariableJson((AutomationVariable)newAsset));
                }
            }

            // add / update credentials
            foreach (var newAsset in newAssets)
            {
                if (!(newAsset is AutomationCredential))
                {
                    continue;
                }
                
                bool found = false;
                foreach (var currentLocalAsset in localAssets.PSCredentials)
                {
                    if (newAsset.Name == currentLocalAsset.Name)
                    {
                        found = true;
                        currentLocalAsset.Update(newAsset);
                    }
                }

                if (!found)
                {
                    localAssets.PSCredentials.Add(new CredentialJson((AutomationCredential)newAsset));
                }
            }

            // add / update connections
            foreach (var newAsset in newAssets)
            {
                if (!(newAsset is AutomationConnection))
                {
                    continue;
                }

                bool found = false;
                foreach (var currentLocalAsset in localAssets.Connections)
                {
                    if (newAsset.Name == currentLocalAsset.Name)
                    {
                        found = true;
                        currentLocalAsset.Update(newAsset);
                    }
                }

                if (!found)
                {
                    localAssets.Connections.Add(new ConnectionJson((AutomationConnection)newAsset));
                }
            }

            DirectoryInfo dir = Directory.CreateDirectory(workspacePath);
            UnsecureLocalAssetsContainerJson.Set(workspacePath, localAssets);
            SecureLocalAssetsContainerJson.Set(workspacePath, localAssets); 
        }
        
        public static LocalAssets Get(String workspacePath)
        {
            LocalAssets localAssetsContainer = new LocalAssets(); 
            
            UnsecureLocalAssetsContainerJson localAssetsJson = UnsecureLocalAssetsContainerJson.Get(workspacePath);
            SecureLocalAssetsContainerJson secureLocalAssetsJson = SecureLocalAssetsContainerJson.Get(workspacePath);
            
            // add JSON variables to the container
            localAssetsJson.Variable.ForEach(variable => variable.Encrypted = false);
            localAssetsContainer.Variables.AddRange(localAssetsJson.Variable);

            secureLocalAssetsJson.Variable.ForEach(variable => variable.Encrypted = true);
            localAssetsContainer.Variables.AddRange(secureLocalAssetsJson.Variable);

            // add JSON credentials to the container
            localAssetsContainer.PSCredentials.AddRange(secureLocalAssetsJson.PSCredential);

            // add JSON connections to the container
            localAssetsContainer.Connections.AddRange(secureLocalAssetsJson.Connection);

            return localAssetsContainer;
        }

        private abstract class AbstractLocalAssetsContainerJson
        {
            public List<VariableJson> Variable = new List<VariableJson>();
            public static JavaScriptSerializer jss = new JavaScriptSerializer();

            public static void WriteJson(string jsonFilePath, Object assets) {
                var assetsSerialized = JsonConvert.SerializeObject(assets, Formatting.Indented);
                File.WriteAllText(jsonFilePath, assetsSerialized);
            }
        }

        private class UnsecureLocalAssetsContainerJson
            : AbstractLocalAssetsContainerJson
        {
            public static UnsecureLocalAssetsContainerJson Get(string workspacePath)
            {
                try
                {
                    string localAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.localAssetsFileName); 
                    return jss.Deserialize<UnsecureLocalAssetsContainerJson>(File.ReadAllText(localAssetsFilePath));
                }
                catch
                {
                    return new UnsecureLocalAssetsContainerJson();
                }
            }

            public static void Set(string workspacePath, LocalAssets localAssets)
            {
                var localAssetsUnsecure = new UnsecureLocalAssetsContainerJson();
                foreach (var localVariableAsset in localAssets.Variables)
                {
                    if (!localVariableAsset.Encrypted)
                    {
                        localAssetsUnsecure.Variable.Add(localVariableAsset);
                    }
                }

                WriteJson(System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.localAssetsFileName), localAssetsUnsecure);
            }
           
            //public List<CertificateJson> Certificate = new List<CertificateJson>();
        }

        private class SecureLocalAssetsContainerJson
            : AbstractLocalAssetsContainerJson
        {
            public static SecureLocalAssetsContainerJson Get(string workspacePath)
            {
                try
                {
                    string secureLocalAssetsFilePath = System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.secureLocalAssetsFileName);
                    return jss.Deserialize<SecureLocalAssetsContainerJson>(File.ReadAllText(secureLocalAssetsFilePath));
                }
                catch
                {
                    return new SecureLocalAssetsContainerJson();
                }
            }

            public static void Set(string workspacePath, LocalAssets localAssets)
            {
                var localAssetsSecure = new SecureLocalAssetsContainerJson();
                foreach (var localVariableAsset in localAssets.Variables)
                {
                    if (localVariableAsset.Encrypted)
                    {
                        localAssetsSecure.Variable.Add(localVariableAsset);
                    }
                }

                localAssetsSecure.PSCredential.AddRange(localAssets.PSCredentials);

                localAssetsSecure.Connection.AddRange(localAssets.Connections);

                WriteJson(System.IO.Path.Combine(workspacePath, AutomationISE.Model.Constants.secureLocalAssetsFileName), localAssetsSecure); 
            }

            public List<CredentialJson> PSCredential = new List<CredentialJson>();
            public List<ConnectionJson> Connection = new List<ConnectionJson>();
        }

    }
}