﻿/*
 * Process Hacker - 
 *   windows API wrapper code
 * 
 * Copyright (C) 2009 Dean
 * Copyright (C) 2008-2009 wj32
 * 
 * This file is part of Process Hacker.
 * 
 * Process Hacker is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Process Hacker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Process Hacker.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Net;

namespace ProcessHacker
{
    /// <summary>
    /// Provides interfacing to the Win32 and Native APIs.
    /// </summary>
    public partial class Win32
    {
        static Win32()
        {
            RefreshDriveDevicePrefixes();
        }

        /// <summary>
        /// Contains code which uses pointers.
        /// </summary>
        public unsafe class Unsafe
        {
            /// <summary>
            /// Converts a multi-string into a managed string array. A multi-string 
            /// consists of an array of null-terminated strings plus an extra null to 
            /// terminate the array.
            /// </summary>
            /// <param name="ptr">The pointer to the array.</param>
            /// <returns>A string array.</returns>
            public static string[] GetMultiString(IntPtr ptr)
            {
                List<string> list = new List<string>();
                char* chptr = (char*)ptr.ToPointer();
                StringBuilder currentString = new StringBuilder();

                while (true)
                {
                    while (*chptr != 0)
                    {
                        currentString.Append(*chptr);  
                        chptr++;
                    }

                    string str = currentString.ToString();

                    if (str == "")
                    {
                        break;
                    }
                    else
                    {
                        list.Add(str);
                        currentString = new StringBuilder();
                    }
                }

                return list.ToArray();
            }

            public static int SingleToInt32(float f)
            {
                return *(int*)&f;
            }

            public static long DoubleToInt64(double d)
            {
                return *(long*)&d;
            }

            public static float Int32ToSingle(int i)
            {
                return *(float*)&i;
            }

            public static double Int64ToDouble(long l)
            {
                return *(double*)&l;
            }
        }

        public delegate int EnumWindowsProc(int hwnd, int param);
        public delegate int SymEnumSymbolsProc(SYMBOL_INFO pSymInfo, int SymbolSize, int UserContext);
        public delegate int FunctionTableAccessProc64(int ProcessHandle, int AddrBase);
        public delegate int GetModuleBaseProc64(int ProcessHandle, int Address);

        /// <summary>
        /// A cache for type names; QuerySystemInformation with ALL_TYPES_INFORMATION fails for some 
        /// reason. The dictionary relates object type numbers to their names.
        /// </summary>
        public static Dictionary<byte, string> ObjectTypes = new Dictionary<byte, string>();

        /// <summary>
        /// Used to resolve device prefixes (\Device\Harddisk1) into DOS drive names.
        /// </summary>
        public static Dictionary<string, string> DriveDevicePrefixes = new Dictionary<string, string>();

        #region Consts

        public const int ANYSIZE_ARRAY = 1;
        public const int DONT_RESOLVE_DLL_REFERENCES = 0x1;
        public const int ERROR_NO_MORE_ITEMS = 259;
        public const int MAXIMUM_SUPPORTED_EXTENSION = 512;
        public const int SEE_MASK_INVOKEIDLIST = 0xc;
        public const uint SERVICE_NO_CHANGE = 0xffffffff;
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const int SID_SIZE = 0x1000;
        public const int SIZE_OF_80387_REGISTERS = 72;
        public const uint STATUS_INFO_LENGTH_MISMATCH = 0xc0000004;
        public const int SW_SHOW = 5;
        public const int SYMBOL_NAME_MAXSIZE = 255;
        public const int WAIT_ABANDONED = 0x80;
        public const int WAIT_OBJECT_0 = 0x0;
        public const int WAIT_TIMEOUT = 0x102;

        #endregion    

        #region Cryptography

        public enum VerifyResult
        {
            Trusted = 0,
            TrustedInstaller,
            NoSignature,
            Expired,
            Revoked,
            Distrust,
            SecuritySettings
        }

        public static readonly GUID WINTRUST_ACTION_GENERIC_CERT_VERIFY = new GUID()
        {
            Data1 = 0x189a3842,
            Data2 = 0x3041,
            Data3 = 0x11d1,
            Data4 = new byte[] { 0x85, 0xe1, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee }
        };

        public static readonly GUID WINTRUST_ACTION_GENERIC_VERIFY_V2 = new GUID()
        {
            Data1 = 0xaac56b,
            Data2 = 0xcd44,
            Data3 = 0x11d0,
            Data4 = new byte[] { 0x8c, 0xc2, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee }
        };

        public static readonly GUID WINTRUST_ACTION_GENERIC_CHAIN_VERIFY = new GUID()
        {
            Data1 = 0xfc451c16,
            Data2 = 0xac75,
            Data3 = 0x11d1,
            Data4 = new byte[] { 0xb4, 0xb8, 0x00, 0xc0, 0x4f, 0xb6, 0x6e, 0xa0 }
        };

        public static readonly GUID DRIVER_ACTION_VERIFY = new GUID()
        {
            Data1 = 0xf750e6c3,
            Data2 = 0x38ee,
            Data3 = 0x11d1,
            Data4 = new byte[] { 0x85, 0xe5, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee }
        };

        public static VerifyResult StatusToVerifyResult(uint status)
        {
            if (status == 0)
                return VerifyResult.Trusted;
            else if (status == 0x800b0100)
                return VerifyResult.NoSignature;
            else if (status == 0x800b0101)
                return VerifyResult.Expired;
            else if (status == 0x800b010c)
                return VerifyResult.Revoked;
            else if (status == 0x800b0111)
                return VerifyResult.Distrust;
            else if (status == 0x80092026)
                return VerifyResult.SecuritySettings;
            else
                return VerifyResult.SecuritySettings;
        }

        public static VerifyResult VerifyFile(string filePath)
        {
            VerifyResult result = VerifyResult.NoSignature;

            using (MemoryAlloc strMem = new MemoryAlloc(filePath.Length * 2 + 2))
            {
                WINTRUST_FILE_INFO fileInfo = new WINTRUST_FILE_INFO();

                strMem.WriteUnicodeString(0, filePath);
                strMem.WriteByte(filePath.Length * 2, 0);
                strMem.WriteByte(filePath.Length * 2 + 1, 0);

                fileInfo.Size = Marshal.SizeOf(fileInfo);
                fileInfo.FilePath = strMem;

                WINTRUST_DATA trustData = new WINTRUST_DATA();

                trustData.Size = 12 * 4;
                trustData.UIChoice = 2; // WTD_UI_NONE
                trustData.UnionChoice = 1; // WTD_CHOICE_FILE
                trustData.ProvFlags = 0x100; // WTD_SAFER_FLAG

                using (MemoryAlloc mem = new MemoryAlloc(fileInfo.Size))
                {
                    Marshal.StructureToPtr(fileInfo, mem, false);
                    trustData.UnionData = mem;

                    GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
                    uint winTrustResult = WinVerifyTrust(0, ref action, ref trustData);

                    result = StatusToVerifyResult(winTrustResult);
                }
            }

            if (result == VerifyResult.NoSignature)
            {
                #region Old Code
                // check the file's permissions
                //try
                //{
                //    System.IO.FileInfo info = new System.IO.FileInfo(filePath);
                //    bool othersCanWrite = false; // if accounts other than TrustedInstaller can write 

                //    // TrustedInstaller must own the file
                //    if (info.GetAccessControl().GetOwner(typeof(System.Security.Principal.NTAccount)).Value !=
                //        "NT SERVICE\\TrustedInstaller")
                //    {
                //        result = VerifyResult.NoSignature;
                //    }
                //    else
                //    {
                //        foreach (System.Security.AccessControl.FileSystemAccessRule rule in
                //            info.GetAccessControl().GetAccessRules(true, true,
                //            typeof(System.Security.Principal.NTAccount)))
                //        {
                //            if (rule.IdentityReference.Value == "NT SERVICE\\TrustedInstaller")
                //            {
                //                result = VerifyResult.TrustedInstaller;
                //            }
                //            else
                //            {
                //                // if any accounts other than TrustedInstaller can write to the file, then it's
                //                // not a real Windows component.
                //                if (rule.AccessControlType == System.Security.AccessControl.AccessControlType.Allow &&
                //                    (rule.FileSystemRights & (
                //                    System.Security.AccessControl.FileSystemRights.ChangePermissions |
                //                    System.Security.AccessControl.FileSystemRights.Delete |
                //                    System.Security.AccessControl.FileSystemRights.DeleteSubdirectoriesAndFiles |
                //                    System.Security.AccessControl.FileSystemRights.TakeOwnership |
                //                    System.Security.AccessControl.FileSystemRights.Write)) != 0)
                //                {
                //                    othersCanWrite = true;
                //                    break;
                //                }
                //            }
                //        }

                //        if (othersCanWrite)
                //            result = VerifyResult.NoSignature;
                //    }
                //}
                //catch
                //{ }
                #endregion

                FileHandle sourceFile = new FileHandle(filePath, FILE_RIGHTS.FILE_GENERIC_READ, FILE_SHARE_MODE.Read,
                    FILE_CREATION_DISPOSITION.OpenExisting);
                byte[] hash = new byte[256];
                int hashLength = 256;

                if (!CryptCATAdminCalcHashFromFileHandle(sourceFile, ref hashLength, hash, 0))
                    return VerifyResult.NoSignature;

                StringBuilder memberTag = new StringBuilder();

                for (int i = 0; i < hashLength; i++)
                    memberTag.Append(hash[i].ToString("X2"));

                int catAdmin;
                GUID action = DRIVER_ACTION_VERIFY;

                if (!CryptCATAdminAcquireContext(out catAdmin, ref action, 0))
                    return VerifyResult.NoSignature;

                int catInfo = CryptCATAdminEnumCatalogFromHash(catAdmin, hash, hashLength, 0, 0);

                if (catInfo == 0)
                {
                    CryptCATAdminReleaseContext(catAdmin, 0);
                    return VerifyResult.NoSignature;
                }

                CATALOG_INFO ci = new CATALOG_INFO();
                CryptCATCatalogInfoFromContext(catInfo, ref ci, 0);

                WINTRUST_CATALOG_INFO wci = new WINTRUST_CATALOG_INFO();

                wci.Size = Marshal.SizeOf(wci);
                wci.CatalogFilePath = ci.CatalogFile;
                wci.MemberFilePath = filePath;
                wci.MemberTag = memberTag.ToString();

                WINTRUST_DATA trustData = new WINTRUST_DATA();

                trustData.Size = 12 * 4;
                trustData.UIChoice = 1;
                trustData.UnionChoice = 2;

                using (MemoryAlloc mem = new MemoryAlloc(wci.Size))
                {
                    Marshal.StructureToPtr(wci, mem, false);
                    trustData.UnionData = mem;

                    GUID action2 = DRIVER_ACTION_VERIFY;
                    uint winTrustResult = WinVerifyTrust(0, ref action2, ref trustData);

                    result = StatusToVerifyResult(winTrustResult);
                    CryptCATAdminReleaseCatalogContext(catAdmin, catInfo, 0);
                }
            }

            return result;
        }

        #endregion

        #region Errors

        /// <summary>
        /// Gets the error message associated with the specified error code.
        /// </summary>
        /// <param name="ErrorCode">The error code.</param>
        /// <returns>An error message.</returns>
        public static string GetErrorMessage(int ErrorCode)
        {
            StringBuilder buffer = new StringBuilder(0x100);

            if (FormatMessage(0x3200, 0, ErrorCode, 0, buffer, buffer.Capacity, IntPtr.Zero) == 0)
                return "Unknown error (0x" + ErrorCode.ToString("x") + ")";

            StringBuilder result = new StringBuilder();
            int i = 0;

            while (i < buffer.Length)
            {
                if (buffer[i] == '\0')
                    break;

                result.Append(buffer[i]);
                i++;
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the error message associated with the last error that occured.
        /// </summary>
        /// <returns>An error message.</returns>
        public static string GetLastErrorMessage()
        {
            return GetErrorMessage(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Throws a Win32Exception with the last error that occurred.
        /// </summary>
        public static void ThrowLastWin32Error()
        {
            int error = Marshal.GetLastWin32Error();

            if (error != 0)
                throw new WindowsException(error);
        }

        #endregion

        #region Files

        public static void RefreshDriveDevicePrefixes()
        {
            lock (DriveDevicePrefixes)
            {
                DriveDevicePrefixes.Clear();

                for (char c = 'A'; c <= 'Z'; c++)
                {
                    StringBuilder target = new StringBuilder(1024);

                    if (QueryDosDevice(c.ToString() + ":", target, 1024) != 0)
                    {
                        DriveDevicePrefixes.Add(target.ToString(), c.ToString() + ":");
                    }
                }
            }
        }

        #endregion

        #region Handles

        public struct ObjectInformation
        {
            public OBJECT_BASIC_INFORMATION Basic;
            public OBJECT_NAME_INFORMATION Name;
            public string OrigName;
            public string BestName;
            public string TypeName;
        }

        /// <summary>
        /// Enumerates the handles opened by every running process.
        /// </summary>
        /// <returns>An array containing information about the handles.</returns>
        public static SYSTEM_HANDLE_INFORMATION[] EnumHandles()
        {
            int retLength = 0;
            int handleCount = 0;
            SYSTEM_HANDLE_INFORMATION[] returnHandles;

            using (MemoryAlloc data = new MemoryAlloc(0x1000))
            {
                // This is needed because ZwQuerySystemInformation with SystemHandleInformation doesn't 
                // actually give a real return length when called with an insufficient buffer. This code 
                // tries repeatedly to call the function, doubling the buffer size each time it fails.
                while (ZwQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemHandleInformation, data.Memory,
                    data.Size, out retLength) == STATUS_INFO_LENGTH_MISMATCH)
                    data.Resize(data.Size * 2);

                // The structure of the buffer is the handle count plus an array of SYSTEM_HANDLE_INFORMATION 
                // structures.
                handleCount = data.ReadInt32(0);
                returnHandles = new SYSTEM_HANDLE_INFORMATION[handleCount];

                for (int i = 0; i < handleCount; i++)
                {
                    returnHandles[i] = data.ReadStruct<SYSTEM_HANDLE_INFORMATION>(4, i);
                }

                return returnHandles;
            }
        }

        public static ObjectInformation GetHandleInfo(SYSTEM_HANDLE_INFORMATION handle)
        {
            using (ProcessHandle process = new ProcessHandle(handle.ProcessId, PROCESS_RIGHTS.PROCESS_DUP_HANDLE))
            {
                return GetHandleInfo(process, handle);
            }
        }

        public static ObjectInformation GetHandleInfo(ProcessHandle process, SYSTEM_HANDLE_INFORMATION handle)
        {
            int object_handle;
            int retLength = 0;

            // duplicates the handle so we can query it
            if (ZwDuplicateObject(process.Handle, handle.Handle,
                Program.CurrentProcess, out object_handle, 0, 0, 0) != 0)
                throw new Exception("Could not duplicate object!");

            try
            {
                ObjectInformation info = new ObjectInformation();

                ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectBasicInformation,
                    IntPtr.Zero, 0, out retLength);

                if (retLength > 0)
                {
                    using (MemoryAlloc obiMem = new MemoryAlloc(retLength))
                    {
                        ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectBasicInformation,
                            obiMem.Memory, obiMem.Size, out retLength);

                        OBJECT_BASIC_INFORMATION obi = obiMem.ReadStruct<OBJECT_BASIC_INFORMATION>();
                        info.Basic = obi;
                    }
                }

                // If the cache contains the object type's name, use it. Otherwise, query the type 
                // for its name.
                if (ObjectTypes.ContainsKey(handle.ObjectTypeNumber))
                {
                    info.TypeName = ObjectTypes[handle.ObjectTypeNumber];
                }
                else
                {
                    ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                        IntPtr.Zero, 0, out retLength);

                    if (retLength > 0)
                    {
                        using (MemoryAlloc otiMem = new MemoryAlloc(retLength))
                        {
                            if (ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                                otiMem.Memory, otiMem.Size, out retLength) != 0)
                                throw new Exception("ZwQueryObject failed");

                            OBJECT_TYPE_INFORMATION oti = otiMem.ReadStruct<OBJECT_TYPE_INFORMATION>();

                            info.TypeName = ReadUnicodeString(oti.Name);
                            ObjectTypes.Add(handle.ObjectTypeNumber, info.TypeName);
                        }
                    }
                }

                if (Program.KPH != null && info.TypeName == "File")
                {
                    // use KProcessHacker for files
                    info.OrigName = Program.KPH.GetObjectName(handle);
                }
                else if (info.TypeName == "File" && (int)handle.GrantedAccess == 0x0012019f)
                {
                    // KProcessHacker not available, fall back to using hack
                }
                else
                {
                    ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                        IntPtr.Zero, 0, out retLength);

                    if (retLength > 0)
                    {
                        using (MemoryAlloc oniMem = new MemoryAlloc(retLength))
                        {
                            if (ZwQueryObject(object_handle, OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                                oniMem.Memory, oniMem.Size, out retLength) != 0)
                                throw new Exception("ZwQueryObject failed");

                            OBJECT_NAME_INFORMATION oni = oniMem.ReadStruct<OBJECT_NAME_INFORMATION>();

                            info.OrigName = ReadUnicodeString(oni.Name);
                            info.Name = oni;
                        }
                    }
                }

                // get a better name for the handle
                try
                {
                    switch (info.TypeName)
                    {
                        case "File":
                            // resolves \Device\Harddisk1 into C:, for example
                            lock (DriveDevicePrefixes)
                            {
                                foreach (var pair in DriveDevicePrefixes)
                                    if (info.OrigName.StartsWith(pair.Key))
                                        info.BestName = pair.Value + info.OrigName.Substring(pair.Key.Length);

                                if (info.BestName == null)
                                    info.BestName = info.OrigName;
                            }

                            break;

                        case "Key":
                            string hklmString = "\\registry\\machine";
                            string hkcrString = "\\registry\\machine\\software\\classes";
                            string hkcuString = "\\registry\\user\\" +
                                System.Security.Principal.WindowsIdentity.GetCurrent().User.ToString().ToLower();
                            string hkcucrString = "\\registry\\user\\" +
                                System.Security.Principal.WindowsIdentity.GetCurrent().User.ToString().ToLower() + "_classes";
                            string hkuString = "\\registry\\user";
                                                       
                            if (info.OrigName.ToLower().StartsWith(hkcrString))
                                info.BestName = "HKCR" + info.OrigName.Substring(hkcrString.Length);
                            else if (info.OrigName.ToLower().StartsWith(hklmString))
                                info.BestName = "HKLM" + info.OrigName.Substring(hklmString.Length);
                            else if (info.OrigName.ToLower().StartsWith(hkcucrString))
                                info.BestName = "HKCU\\Software\\Classes" + info.OrigName.Substring(hkcucrString.Length);
                            else if (info.OrigName.ToLower().StartsWith(hkcuString))
                                info.BestName = "HKCU" + info.OrigName.Substring(hkcuString.Length);
                            else if (info.OrigName.ToLower().StartsWith(hkuString))
                                info.BestName = "HKU" + info.OrigName.Substring(hkuString.Length);
                            else
                                info.BestName = info.OrigName;

                            break;

                        case "Process":
                            {
                                int process_handle;
                                int processId;

                                if (ZwDuplicateObject(process.Handle, handle.Handle,
                                    Program.CurrentProcess, out process_handle,
                                    (STANDARD_RIGHTS)Program.MinProcessQueryRights, 0, 0) != 0)
                                    throw new Exception("Could not duplicate process handle!");

                                try
                                {
                                    if ((processId = GetProcessId(process_handle)) == 0)
                                        ThrowLastWin32Error();

                                    if (Program.HackerWindow.ProcessProvider.Dictionary.ContainsKey(processId))
                                        info.BestName = Program.HackerWindow.ProcessProvider.Dictionary[processId].Name +
                                            " (" + processId.ToString() + ")";
                                    else
                                        info.BestName = "Non-existent process (" + processId.ToString() + ")";
                                }
                                finally
                                {
                                    CloseHandle(process_handle);
                                }
                            }

                            break;

                        case "Thread":
                            {
                                int thread_handle;
                                int processId;
                                int threadId;

                                if (ZwDuplicateObject(process.Handle, handle.Handle,
                                    Program.CurrentProcess, out thread_handle,
                                    (STANDARD_RIGHTS)Program.MinThreadQueryRights, 0, 0) != 0)
                                    throw new Exception("Could not duplicate thread handle!");

                                try
                                {
                                    if ((threadId = GetThreadId(thread_handle)) == 0)
                                        ThrowLastWin32Error();

                                    if ((processId = GetProcessIdOfThread(thread_handle)) == 0)
                                        ThrowLastWin32Error();

                                    if (Program.HackerWindow.ProcessProvider.Dictionary.ContainsKey(processId))
                                        info.BestName = Program.HackerWindow.ProcessProvider.Dictionary[processId].Name +
                                            " (" + processId.ToString() + "): " + threadId.ToString();
                                    else
                                        info.BestName = "Non-existent process (" + processId.ToString() + "): " +
                                            threadId.ToString();
                                }
                                finally
                                {
                                    CloseHandle(thread_handle);
                                }
                            }

                            break;

                        case "Token":
                            {
                                int token_handle;

                                if (ZwDuplicateObject(process.Handle, handle.Handle,
                                    Program.CurrentProcess, out token_handle,
                                    (STANDARD_RIGHTS)TOKEN_RIGHTS.TOKEN_QUERY, 0, 0) != 0)
                                    throw new Exception("Could not duplicate token handle!");

                                try
                                {
                                    info.BestName = TokenHandle.FromHandle(token_handle).GetUser().GetName(true);
                                }
                                finally
                                {
                                    CloseHandle(token_handle);
                                }
                            }

                            break;

                        default:
                            if (info.OrigName != null &&
                                info.OrigName != "")
                            {
                                info.BestName = info.OrigName;
                            }
                            else
                            {
                                info.BestName = null;
                            }

                            break;
                    }
                }
                catch
                {
                    if (info.OrigName != null &&
                                info.OrigName != "")
                    {
                        info.BestName = info.OrigName;
                    }
                    else
                    {
                        info.BestName = null;
                    }
                }

                return info;
            }
            finally
            {
                CloseHandle(object_handle);
            }

            // this means that for some reason, the return statement above didn't execute.
            throw new Exception("Failed");
        }

        #endregion

        #region Misc.

        /// <summary>
        /// Loads an image into kernel-mode using ZwSetSystemInformation 
        /// with SystemLoadAndCallImage.
        /// </summary>
        /// <param name="fileName">The path to the driver.</param>
        public static void LoadKernelImage(string fileName)
        {
            System.IO.FileInfo info = new System.IO.FileInfo(fileName);
            string ntFileName = "\\??\\" + info.FullName;

            SYSTEM_LOAD_AND_CALL_IMAGE laci = new SYSTEM_LOAD_AND_CALL_IMAGE();

            using (MemoryAlloc stringData = new MemoryAlloc(ntFileName.Length * 2 + 2))
            {
                laci.ModuleName = new UNICODE_STRING();

                stringData.WriteUnicodeString(0, ntFileName);
                laci.ModuleName.Buffer = stringData;
                laci.ModuleName.Length = (ushort)(ntFileName.Length * 2);
                laci.ModuleName.MaximumLength = laci.ModuleName.Length;

                uint ret;

                if ((ret = ZwSetSystemInformation(SYSTEM_INFORMATION_CLASS.SystemLoadAndCallImage,
                    ref laci, Marshal.SizeOf(laci))) != 0)
                    throw new Exception("Failed to load the kernel image - error " + ret.ToString());
            }
        }

        /// <summary>
        /// Reads a Unicode string.
        /// </summary>
        /// <param name="str">A UNICODE_STRING structure.</param>
        /// <returns>A string.</returns>
        /// <remarks>This function is needed because some LSA strings are not 
        /// null-terminated, so we can't use .NET's marshalling.</remarks>
        public static string ReadUnicodeString(UNICODE_STRING str)
        {
            if (str.Length == 0)
                return null;

            return Marshal.PtrToStringUni(new IntPtr(str.Buffer), str.Length / 2);
        } 

        public static void ShowProperties(string fileName)
        {
            Win32.SHELLEXECUTEINFO info = new Win32.SHELLEXECUTEINFO();

            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.SHELLEXECUTEINFO));
            info.lpFile = fileName;
            info.nShow = Win32.SW_SHOW;
            info.fMask = Win32.SEE_MASK_INVOKEIDLIST;
            info.lpVerb = "properties";

            Win32.ShellExecuteEx(ref info);
        }

        #endregion

        #region Network

        public enum NetworkProtocol
        {
            Tcp, Udp
        }

        public struct NetworkConnection
        {
            public string ID;
            public int PID;
            public NetworkProtocol Protocol;
            public string LocalString;
            public IPEndPoint Local;
            public string RemoteString;
            public IPEndPoint Remote;
            public MIB_TCP_STATE State;
        }

        public static Dictionary<int, List<NetworkConnection>> GetNetworkConnections()
        {
            var retDict = new Dictionary<int, List<NetworkConnection>>();
            int length = 0;

            GetExtendedTcpTable(IntPtr.Zero, ref length, false, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

            using (var mem = new MemoryAlloc(length))
            {
                GetExtendedTcpTable(mem, ref length, false, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

                int count = mem.ReadInt32(0);

                for (int i = 0; i < count; i++)
                {
                    var struc = mem.ReadStruct<MIB_TCPROW_OWNER_PID>(4, i);

                    if (!retDict.ContainsKey(struc.OwningProcessId))
                        retDict.Add(struc.OwningProcessId, new List<NetworkConnection>());

                    retDict[struc.OwningProcessId].Add(new NetworkConnection()
                        {
                            Protocol = NetworkProtocol.Tcp,
                            Local = new IPEndPoint(struc.LocalAddress, Misc.ByteSwap((ushort)struc.LocalPort)),
                            Remote = new IPEndPoint(struc.RemoteAddress, Misc.ByteSwap((ushort)struc.RemotePort)),
                            State = struc.State,
                            PID = struc.OwningProcessId
                        });
                }
            }

            GetExtendedUdpTable(IntPtr.Zero, ref length, false, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

            using (var mem = new MemoryAlloc(length))
            {
                GetExtendedUdpTable(mem, ref length, false, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

                int count = mem.ReadInt32(0);

                for (int i = 0; i < count; i++)
                {
                    var struc = mem.ReadStruct<MIB_UDPROW_OWNER_PID>(4, i);

                    if (!retDict.ContainsKey(struc.OwningProcessId))
                        retDict.Add(struc.OwningProcessId, new List<NetworkConnection>());

                    retDict[struc.OwningProcessId].Add(
                        new NetworkConnection()
                        {
                            Protocol = NetworkProtocol.Udp,
                            Local = new IPEndPoint(struc.LocalAddress, Misc.ByteSwap((ushort)struc.LocalPort)),
                            PID = struc.OwningProcessId
                        });
                }
            }

            return retDict;
        }

        #endregion

        #region Processes

        public struct SystemProcess
        {
            public string Name;
            public SYSTEM_PROCESS_INFORMATION Process;
            public Dictionary<int, SYSTEM_THREAD_INFORMATION> Threads;
        }

        /// <summary>
        /// Specifies the processes which should have their threads filled in. This is
        /// used as a performance boost.
        /// </summary>
        public static Dictionary<int, object> ProcessesWithThreads
            = new Dictionary<int, object>();

        /// <summary>
        /// Gets a dictionary containing the currently running processes.
        /// </summary>
        /// <returns>A dictionary, indexed by process ID.</returns>
        public static Dictionary<int, SystemProcess> EnumProcesses()
        {
            int retLength = 0;
            Dictionary<int, SystemProcess> returnProcesses;

            ZwQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemProcessesAndThreadsInformation, IntPtr.Zero,
                0, out retLength);

            using (MemoryAlloc data = new MemoryAlloc(retLength))
            {
                ZwQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemProcessesAndThreadsInformation, data.Memory,
                    data.Size, out retLength);

                returnProcesses = new Dictionary<int, SystemProcess>();

                int i = 0;
                SystemProcess currentProcess = new SystemProcess();

                while (true)
                {
                    currentProcess.Process = data.ReadStruct<SYSTEM_PROCESS_INFORMATION>(i, 0);
                    currentProcess.Name = ReadUnicodeString(currentProcess.Process.ImageName);

                    if (ProcessesWithThreads.ContainsKey(currentProcess.Process.ProcessId) && 
                        currentProcess.Process.ProcessId != 0)
                    {
                        currentProcess.Threads = new Dictionary<int, SYSTEM_THREAD_INFORMATION>();

                        for (int j = 0; j < currentProcess.Process.NumberOfThreads; j++)
                        {
                            Win32.SYSTEM_THREAD_INFORMATION thread = data.ReadStruct<SYSTEM_THREAD_INFORMATION>(i +
                                Marshal.SizeOf(typeof(SYSTEM_PROCESS_INFORMATION)), j);

                            currentProcess.Threads.Add(thread.ClientId.UniqueThread, thread);
                        }
                    }

                    returnProcesses.Add(currentProcess.Process.ProcessId, currentProcess);

                    if (currentProcess.Process.NextEntryOffset == 0)
                        break;

                    i += currentProcess.Process.NextEntryOffset;
                }

                return returnProcesses;
            }
        }   

        public static Icon GetFileIcon(string fileName)
        {
            return GetFileIcon(fileName, false);
        }

        public static Icon GetFileIcon(string fileName, bool large)
        {
            Win32.SHFILEINFO shinfo = new Win32.SHFILEINFO();

            if (fileName == null || fileName == "")
                throw new Exception("File name cannot be empty.");

            try
            {
                if (Win32.SHGetFileInfo(fileName, 0, ref shinfo,
                      (uint)Marshal.SizeOf(shinfo),
                       Win32.SHGFI_ICON |
                       (large ? Win32.SHGFI_LARGEICON : Win32.SHGFI_SMALLICON)) == 0)
                {
                    return null;
                }
                else
                {
                    return Icon.FromHandle(shinfo.hIcon);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the name of the process with the specified process ID.
        /// </summary>
        /// <param name="pid">The ID of the process to search for.</param>
        /// <returns>The name of the process</returns>
        public static string GetNameFromPID(int pid)
        {
            PROCESSENTRY32 proc = new PROCESSENTRY32();
            int snapshot = 0;

            snapshot = CreateToolhelp32Snapshot(SnapshotFlags.Process, pid);

            if (snapshot == 0)
                return "(error)";

            proc.dwSize = Marshal.SizeOf(typeof(PROCESSENTRY32));

            Process32First(snapshot, ref proc);

            do
            {
                if (proc.th32ProcessID == pid)
                {
                    if (proc.szExeFile != "[System Process]")
                        return proc.szExeFile;
                    else
                        return "System Idle Process";
                }
            } while (Process32Next(snapshot, ref proc) != 0);

            return "(unknown)";
        }

        public static int GetProcessSessionId(int ProcessId)
        {
            int sessionId = -1;

            try
            {
                if (!ProcessIdToSessionId(ProcessId, out sessionId))
                    ThrowLastWin32Error();
            }
            catch
            {
                using (ProcessHandle phandle = new ProcessHandle(ProcessId, Program.MinProcessQueryRights))
                {
                    return phandle.GetToken(TOKEN_RIGHTS.TOKEN_QUERY).GetSessionId();
                }
            }

            return sessionId;
        }

        #endregion

        #region Security

        public static string GetAccountName(int SID, bool IncludeDomain)
        {
            StringBuilder name = new StringBuilder(255);
            StringBuilder domain = new StringBuilder(255);
            int namelen = 255;
            int domainlen = 255;
            SID_NAME_USE use = SID_NAME_USE.SidTypeUser;

            try
            {
                if (!LookupAccountSid(null, SID, name, out namelen, domain, out domainlen, out use))
                {
                    // if the name is longer than 255 characters, increase the capacity.
                    name.EnsureCapacity(namelen);
                    domain.EnsureCapacity(domainlen);

                    if (!LookupAccountSid(null, SID, name, out namelen, domain, out domainlen, out use))
                    {
                        if (name.ToString() == "" && domain.ToString() == "")
                            throw new Exception("Could not lookup account SID: " + Win32.GetLastErrorMessage());
                    }
                }
            }
            catch
            {
                // if we didn't find a name, then return the string SID version.
                return GetAccountStringSID(SID);
            }

            if (IncludeDomain)
            {
                return ((domain.ToString() != "") ? domain.ToString() + "\\" : "") + name.ToString();
            }
            else
            {
                return name.ToString();
            }
        }

        public static string GetAccountStringSID(int SID)
        {
            return new System.Security.Principal.SecurityIdentifier(new IntPtr(SID)).ToString();
        }

        public static SID_NAME_USE GetAccountType(int SID)
        {
            StringBuilder name = new StringBuilder(255);
            StringBuilder domain = new StringBuilder(255);
            int namelen = 255;
            int domainlen = 255;
            SID_NAME_USE use = SID_NAME_USE.SidTypeUser;

            // we don't actually need to get the account name
            if (!LookupAccountSid(null, SID, name, out namelen, domain, out domainlen, out use))
            {
                name.EnsureCapacity(namelen);
                domain.EnsureCapacity(domainlen);

                if (!LookupAccountSid(null, SID, name, out namelen, domain, out domainlen, out use))
                {
                    if (name.ToString() == "" && domain.ToString() == "")
                        throw new Exception("Could not lookup account SID: " + Win32.GetLastErrorMessage());
                }
            }

            return use;
        }

        public static string GetPrivilegeDisplayName(string PrivilegeName)
        {
            StringBuilder sb = null;
            int size = 0;
            int languageId = 0;

            LookupPrivilegeDisplayName(0, PrivilegeName, sb, out size, out languageId);
            sb = new StringBuilder(size);
            LookupPrivilegeDisplayName(0, PrivilegeName, sb, out size, out languageId);

            return sb.ToString();
        }

        public static string GetPrivilegeName(LUID Luid)
        {
            StringBuilder sb = null;
            int size = 0;

            LookupPrivilegeName(0, ref Luid, sb, out size);
            sb = new StringBuilder(size);
            LookupPrivilegeName(0, ref Luid, sb, out size);

            return sb.ToString();
        }

        public static TOKEN_PRIVILEGES ReadTokenPrivileges(TokenHandle TokenHandle)
        {
            int retLen = 0;

            GetTokenInformation(TokenHandle.Handle, TOKEN_INFORMATION_CLASS.TokenPrivileges, IntPtr.Zero, 0, out retLen);

            using (MemoryAlloc data = new MemoryAlloc(retLen))
            {
                if (!GetTokenInformation(TokenHandle.Handle, TOKEN_INFORMATION_CLASS.TokenPrivileges, data.Memory,
                    data.Size, out retLen))
                    ThrowLastWin32Error();

                uint number = data.ReadUInt32(0);
                TOKEN_PRIVILEGES privileges = new TOKEN_PRIVILEGES();

                privileges.PrivilegeCount = number;
                privileges.Privileges = new LUID_AND_ATTRIBUTES[number];

                for (int i = 0; i < number; i++)
                {
                    privileges.Privileges[i] = data.ReadStruct<LUID_AND_ATTRIBUTES>(4, i);
                }

                return privileges;
            }
        }

        public static void WriteTokenPrivilege(string PrivilegeName, SE_PRIVILEGE_ATTRIBUTES Attributes)
        {
            WriteTokenPrivilege(
                ProcessHandle.FromHandle(Program.CurrentProcess).GetToken(), PrivilegeName, Attributes);
        }

        public static void WriteTokenPrivilege(TokenHandle TokenHandle, string PrivilegeName, SE_PRIVILEGE_ATTRIBUTES Attributes)
        {
            TOKEN_PRIVILEGES tkp = new TOKEN_PRIVILEGES();

            tkp.Privileges = new LUID_AND_ATTRIBUTES[1];

            if (!LookupPrivilegeValue(null, PrivilegeName, ref tkp.Privileges[0].Luid))
                throw new Exception("Invalid privilege name '" + PrivilegeName + "'.");

            tkp.PrivilegeCount = 1;
            tkp.Privileges[0].Attributes = Attributes;

            AdjustTokenPrivileges(TokenHandle.Handle, 0, ref tkp, 0, 0, 0);

            if (Marshal.GetLastWin32Error() != 0)
                ThrowLastWin32Error();
        }

        #endregion

        #region Services

        public static Dictionary<string, ENUM_SERVICE_STATUS_PROCESS> EnumServices()
        {
            using (ServiceManagerHandle manager =
                new ServiceManagerHandle(SC_MANAGER_RIGHTS.SC_MANAGER_ENUMERATE_SERVICE))
            {
                int requiredSize;
                int servicesReturned;
                int resume;

                // get required size
                EnumServicesStatusEx(manager, 0, SERVICE_QUERY_TYPE.Win32 | SERVICE_QUERY_TYPE.Driver,
                    SERVICE_QUERY_STATE.All, IntPtr.Zero, 0, out requiredSize, out servicesReturned,
                    out resume, 0);

                using (MemoryAlloc data = new MemoryAlloc(requiredSize))
                {
                    Dictionary<string, ENUM_SERVICE_STATUS_PROCESS> dictionary =
                        new Dictionary<string, ENUM_SERVICE_STATUS_PROCESS>();

                    if (!EnumServicesStatusEx(manager, 0, SERVICE_QUERY_TYPE.Win32 | SERVICE_QUERY_TYPE.Driver,
                        SERVICE_QUERY_STATE.All, data,
                        data.Size, out requiredSize, out servicesReturned,
                        out resume, 0))
                    {
                        ThrowLastWin32Error();
                    }

                    for (int i = 0; i < servicesReturned; i++)
                    {
                        ENUM_SERVICE_STATUS_PROCESS service = data.ReadStruct<ENUM_SERVICE_STATUS_PROCESS>(i);

                        dictionary.Add(service.ServiceName, service);
                    }

                    return dictionary;
                }
            }
        }

        public static QUERY_SERVICE_CONFIG GetServiceConfig(string ServiceName)
        {
            using (ServiceHandle service = new ServiceHandle(ServiceName, SERVICE_RIGHTS.SERVICE_QUERY_CONFIG))
            {
                int requiredSize = 0;

                QueryServiceConfig(service, IntPtr.Zero, 0, ref requiredSize);

                using (MemoryAlloc data = new MemoryAlloc(requiredSize))
                {
                    if (!QueryServiceConfig(service, data, data.Size, ref requiredSize))
                        throw new Exception("Could not get service configuration: " + GetLastErrorMessage());

                    return data.ReadStruct<QUERY_SERVICE_CONFIG>();
                }
            }
        }

        #endregion

        #region Statistics

        public static IO_COUNTERS GetProcessIoCounters(ProcessHandle process)
        {
            IO_COUNTERS counters = new IO_COUNTERS();

            if (!GetProcessIoCounters(process.Handle, out counters))
                ThrowLastWin32Error();

            return counters;
        }

        public static ulong[] GetProcessTimes(ProcessHandle process)
        {
            ulong[] times = new ulong[4];

            if (!GetProcessTimes(process.Handle, out times[0], out times[1], out times[2], out times[3]))
                ThrowLastWin32Error();

            return times;
        }

        public static ulong[] GetSystemTimes()
        {
            ulong[] times = new ulong[3];

            if (!GetSystemTimes(out times[0], out times[1], out times[2]))
                ThrowLastWin32Error();

            return times; 
        }

        #endregion

        #region TCP

        public static MIB_TCPSTATS GetTcpStats()
        {
            MIB_TCPSTATS tcpStats = new MIB_TCPSTATS();
            GetTcpStatistics(ref tcpStats);
            return tcpStats;
        }

        public static MIB_TCPTABLE_OWNER_PID GetTcpTable()
        {
            MIB_TCPTABLE_OWNER_PID table = new MIB_TCPTABLE_OWNER_PID();
            int length = 0;

            GetExtendedTcpTable(IntPtr.Zero, ref length, false, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

            using (MemoryAlloc mem = new MemoryAlloc(length))
            {
                GetExtendedTcpTable(mem, ref length, false, 2, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

                int count = mem.ReadInt32(0);

                table.NumEntries = count;
                table.Table = new MIB_TCPROW_OWNER_PID[count];

                for (int i = 0; i < count; i++)
                    table.Table[i] = mem.ReadStruct<MIB_TCPROW_OWNER_PID>(4, i);
            }

            return table;
        }

        #endregion

        #region Terminal Server

        public static WTS_SESSION_INFO[] TSEnumSessions()
        {
            IntPtr sessions;
            int count;
            WTS_SESSION_INFO[] returnSessions;

            WTSEnumerateSessions(0, 0, 1, out sessions, out count);
            returnSessions = new WTS_SESSION_INFO[count];

            WtsMemoryAlloc data = WtsMemoryAlloc.FromPointer(sessions);

            for (int i = 0; i < count; i++)
            {
                returnSessions[i] = data.ReadStruct<WTS_SESSION_INFO>(i);
            }

            data.Dispose();

            return returnSessions;
        }
 
        /// <remarks>
        /// Before we had the WtsMemoryAlloc class, these enumerator 
        /// functions queried LSA about each process' username, 
        /// regardless of whether they were going to be used.
        /// If they didn't do that, the memory allocated for the 
        /// data would be freed and we would end up with invalid 
        /// SID pointers. This structure keeps a WtsMemoryAlloc 
        /// instance alive so that it isn't freed until told to 
        /// do so. This then means that the enumerator functions 
        /// don't need to query LSA so often.
        /// </remarks>
        public struct WtsEnumProcessesData
        {
            public WTS_PROCESS_INFO[] Processes;
            public WtsMemoryAlloc Memory;
        }

        public static WtsEnumProcessesData TSEnumProcesses()
        {
            IntPtr processes;
            int count;
            WTS_PROCESS_INFO[] returnProcesses;

            WTSEnumerateProcesses(0, 0, 1, out processes, out count);
            returnProcesses = new WTS_PROCESS_INFO[count];

            WtsMemoryAlloc data = WtsMemoryAlloc.FromPointer(processes);

            for (int i = 0; i < count; i++)
            {
                returnProcesses[i] = data.ReadStruct<WTS_PROCESS_INFO>(i);
            }

            return new WtsEnumProcessesData() { Processes = returnProcesses, Memory = data };
        }

        public struct WtsEnumProcessesFastData
        {
            public int[] PIDs;
            public int[] SIDs;
            public WtsMemoryAlloc Memory;
        }

        public unsafe static WtsEnumProcessesFastData TSEnumProcessesFast()
        {
            IntPtr processes;
            int count;
            int[] pids;
            int[] sids;

            WTSEnumerateProcesses(0, 0, 1, out processes, out count);

            pids = new int[count];
            sids = new int[count];

            WtsMemoryAlloc data = WtsMemoryAlloc.FromPointer(processes);
            int* dataP = (int*)data.Memory.ToPointer();

            for (int i = 0; i < count; i++)
            {
                pids[i] = dataP[i * 4 + 1];
                sids[i] = dataP[i * 4 + 3];
            }

            return new WtsEnumProcessesFastData() { PIDs = pids, SIDs = sids, Memory = data };
        }

        #endregion

        #region UDP

        public static MIB_UDPSTATS GetUdpStats()
        {
            MIB_UDPSTATS udpStats = new MIB_UDPSTATS();
            GetUdpStatistics(ref udpStats);
            return udpStats;
        }

        public static MIB_UDPTABLE_OWNER_PID GetUdpTable()
        {
            MIB_UDPTABLE_OWNER_PID table = new MIB_UDPTABLE_OWNER_PID();
            int length = 0;

            GetExtendedUdpTable(IntPtr.Zero, ref length, false, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

            using (MemoryAlloc mem = new MemoryAlloc(length))
            {
                GetExtendedUdpTable(mem, ref length, false, 2, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                        
                int count = mem.ReadInt32(0);

                table.NumEntries = count;
                table.Table = new MIB_UDPROW_OWNER_PID[count];

                for (int i = 0; i < count; i++)
                    table.Table[i] = mem.ReadStruct<MIB_UDPROW_OWNER_PID>(4, i);
            }

            return table;
        }

        //GetUdpConnexions()
        //todo 
        //GetExUdpConnexions()
        //todo

        #endregion
    }
}
