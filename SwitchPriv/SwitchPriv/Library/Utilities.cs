﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SwitchPriv.Interop;

namespace SwitchPriv.Library
{
    class Utilities
    {
        public static bool DisableSinglePrivilege(
            IntPtr hToken,
            Win32Struct.LUID priv)
        {
            int error;

            Win32Struct.TOKEN_PRIVILEGES tp = new Win32Struct.TOKEN_PRIVILEGES(1);
            tp.Privileges[0].Luid = priv;
            tp.Privileges[0].Attributes = 0;

            IntPtr pTokenPrivilege = Marshal.AllocHGlobal(Marshal.SizeOf(tp));
            Marshal.StructureToPtr(tp, pTokenPrivilege, true);

            if (!Win32Api.AdjustTokenPrivileges(
                hToken,
                false,
                pTokenPrivilege,
                0,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to disable {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            error = Marshal.GetLastWin32Error();

            if (error != 0)
            {
                Console.WriteLine("[-] Failed to disable {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                
                return false;
            }

            return true;
        }


        public static bool EnableMultiplePrivileges(
            IntPtr hToken,
            string[] privs)
        {
            StringComparison opt = StringComparison.OrdinalIgnoreCase;
            Dictionary<string, bool> results = new Dictionary<string, bool>();
            var privList = new List<string>(privs);
            var availablePrivs = GetAvailablePrivileges(hToken);
            bool isEnabled;
            bool enabledAll = true;

            foreach (var name in privList)
            {
                results.Add(name, false);
            }

            foreach (var priv in availablePrivs)
            {
                foreach (var name in privList)
                {
                    if (string.Compare(Helpers.GetPrivilegeName(priv.Key), name, opt) == 0)
                    {
                        isEnabled = ((priv.Value & (uint)Win32Const.SE_PRIVILEGE_ATTRIBUTES.SE_PRIVILEGE_ENABLED) != 0);

                        if (isEnabled)
                        {
                            results[name] = true;
                        }
                        else
                        {
                            results[name] = EnableSinglePrivilege(hToken, priv.Key);
                        }
                    }
                }
            }

            foreach (var result in results)
            {
                if (!result.Value)
                {
                    Console.WriteLine(
                        "[-] {0} is not available.",
                        result.Key);

                    enabledAll = false;
                }
            }

            return enabledAll;
        }


        public static bool EnableSinglePrivilege(
            IntPtr hToken,
            Win32Struct.LUID priv)
        {
            int error;

            Win32Struct.TOKEN_PRIVILEGES tp = new Win32Struct.TOKEN_PRIVILEGES(1);
            tp.Privileges[0].Luid = priv;
            tp.Privileges[0].Attributes = (uint)Win32Const.PrivilegeAttributeFlags.SE_PRIVILEGE_ENABLED;

            IntPtr pTokenPrivilege = Marshal.AllocHGlobal(Marshal.SizeOf(tp));
            Marshal.StructureToPtr(tp, pTokenPrivilege, true);

            if (!Win32Api.AdjustTokenPrivileges(
                hToken,
                false,
                pTokenPrivilege,
                0,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to enable {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            error = Marshal.GetLastWin32Error();

            if (error != 0)
            {
                Console.WriteLine("[-] Failed to enable {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                
                return false;
            }

            return true;
        }


        public static Dictionary<Win32Struct.LUID, uint> GetAvailablePrivileges(IntPtr hToken)
        {
            int ERROR_INSUFFICIENT_BUFFER = 122;
            int error;
            bool status;
            int bufferLength = Marshal.SizeOf(typeof(Win32Struct.TOKEN_PRIVILEGES));
            Dictionary<Win32Struct.LUID, uint> availablePrivs = new Dictionary<Win32Struct.LUID, uint>();
            IntPtr pTokenPrivileges;

            do
            {
                pTokenPrivileges = Marshal.AllocHGlobal(bufferLength);
                Helpers.ZeroMemory(pTokenPrivileges, bufferLength);

                status = Win32Api.GetTokenInformation(
                    hToken,
                    Win32Const.TOKEN_INFORMATION_CLASS.TokenPrivileges,
                    pTokenPrivileges,
                    bufferLength,
                    out bufferLength);
                error = Marshal.GetLastWin32Error();

                if (!status)
                    Marshal.FreeHGlobal(pTokenPrivileges);
            } while (!status && (error == ERROR_INSUFFICIENT_BUFFER));

            if (!status)
                return availablePrivs;

            int privCount = Marshal.ReadInt32(pTokenPrivileges);
            IntPtr buffer = new IntPtr(pTokenPrivileges.ToInt64() + Marshal.SizeOf(privCount));

            for (var count = 0; count < privCount; count++)
            {
                var luidAndAttr = (Win32Struct.LUID_AND_ATTRIBUTES)Marshal.PtrToStructure(
                    buffer,
                    typeof(Win32Struct.LUID_AND_ATTRIBUTES));

                availablePrivs.Add(luidAndAttr.Luid, luidAndAttr.Attributes);
                buffer = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(luidAndAttr));
            }

            Marshal.FreeHGlobal(pTokenPrivileges);

            return availablePrivs;
        }


        public static string GetIntegrityLevel(IntPtr hToken)
        {
            int ERROR_INSUFFICIENT_BUFFER = 122;
            StringComparison opt = StringComparison.OrdinalIgnoreCase;
            int error;
            bool status;
            int bufferLength = Marshal.SizeOf(typeof(Win32Struct.TOKEN_PRIVILEGES));
            IntPtr pTokenIntegrity;

            do
            {
                pTokenIntegrity = Marshal.AllocHGlobal(bufferLength);
                Helpers.ZeroMemory(pTokenIntegrity, bufferLength);

                status = Win32Api.GetTokenInformation(
                    hToken,
                    Win32Const.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
                    pTokenIntegrity,
                    bufferLength,
                    out bufferLength);
                error = Marshal.GetLastWin32Error();

                if (!status)
                    Marshal.FreeHGlobal(pTokenIntegrity);
            } while (!status && (error == ERROR_INSUFFICIENT_BUFFER));

            if (!status)
                return "N/A";

            var sidAndAttrs = (Win32Struct.SID_AND_ATTRIBUTES)Marshal.PtrToStructure(
                pTokenIntegrity,
                typeof(Win32Struct.SID_AND_ATTRIBUTES));

            if (!Win32Api.ConvertSidToStringSid(sidAndAttrs.Sid, out string strSid))
                return "N/A";

            if (string.Compare(strSid, Win32Const.UNTRUSTED_MANDATORY_LEVEL, opt) == 0)
                return "UNTRUSTED_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.LOW_MANDATORY_LEVEL, opt) == 0)
                return "LOW_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.MEDIUM_MANDATORY_LEVEL, opt) == 0)
                return "MEDIUM_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.MEDIUM_PLUS_MANDATORY_LEVEL, opt) == 0)
                return "MEDIUM_PLUS_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.HIGH_MANDATORY_LEVEL, opt) == 0)
                return "HIGH_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.SYSTEM_MANDATORY_LEVEL, opt) == 0)
                return "SYSTEM_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.PROTECTED_MANDATORY_LEVEL, opt) == 0)
                return "PROTECTED_MANDATORY_LEVEL";
            else if (string.Compare(strSid, Win32Const.SECURE_MANDATORY_LEVEL, opt) == 0)
                return "SECURE_MANDATORY_LEVEL";
            else
                return "N/A";
        }


        public static int GetParentProcessId(IntPtr hProcess)
        {
            var sizeInformation = Marshal.SizeOf(typeof(Win32Struct.PROCESS_BASIC_INFORMATION));
            var buffer = Marshal.AllocHGlobal(sizeInformation);

            if (hProcess == IntPtr.Zero)
                return 0;

            int ntstatus = Win32Api.NtQueryInformationProcess(
                hProcess,
                Win32Const.PROCESSINFOCLASS.ProcessBasicInformation,
                buffer,
                sizeInformation,
                IntPtr.Zero);

            if (ntstatus != Win32Const.STATUS_SUCCESS)
            {
                Console.WriteLine("[-] Failed to get process information.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(ntstatus, true));
                Marshal.FreeHGlobal(buffer);
                
                return 0;
            }

            var basicInfo = (Win32Struct.PROCESS_BASIC_INFORMATION)Marshal.PtrToStructure(
                buffer,
                typeof(Win32Struct.PROCESS_BASIC_INFORMATION));
            int ppid = basicInfo.InheritedFromUniqueProcessId.ToInt32();

            Marshal.FreeHGlobal(buffer);

            return ppid;
        }


        public static bool ImpersonateAsSmss()
        {
            int error;
            int smss;

            Console.WriteLine("[>] Trying to impersonate as smss.exe.");

            try
            {
                smss = (Process.GetProcessesByName("smss")[0]).Id;
            }
            catch
            {
                Console.WriteLine("[-] Failed to get process id of smss.exe.\n");

                return false;
            }

            IntPtr hProcess = Win32Api.OpenProcess(
                Win32Const.ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
                true,
                smss);

            if (hProcess == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to get handle to smss.exe process.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            if (!Win32Api.OpenProcessToken(
                hProcess,
                Win32Const.TokenAccessFlags.TOKEN_DUPLICATE,
                out IntPtr hToken))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to get handle to smss.exe process token.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                Win32Api.CloseHandle(hProcess);

                return false;
            }

            Win32Api.CloseHandle(hProcess);

            if (!Win32Api.DuplicateTokenEx(
                hToken,
                Win32Const.TokenAccessFlags.MAXIMUM_ALLOWED,
                IntPtr.Zero,
                Win32Const.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                Win32Const.TOKEN_TYPE.TokenPrimary,
                out IntPtr hDupToken))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to duplicate smss.exe process token.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                Win32Api.CloseHandle(hToken);

                return false;
            }

            if (!Win32Api.ImpersonateLoggedOnUser(hDupToken))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to impersonate logon user.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                Win32Api.CloseHandle(hDupToken);
                Win32Api.CloseHandle(hToken);

                return false;
            }

            Console.WriteLine("[+] Impersonation is successful.");
            Win32Api.CloseHandle(hDupToken);
            Win32Api.CloseHandle(hToken);

            return true;
        }


        public static bool RemoveSinglePrivilege(IntPtr hToken, Win32Struct.LUID priv)
        {
            int error;

            Win32Struct.TOKEN_PRIVILEGES tp = new Win32Struct.TOKEN_PRIVILEGES(1);
            tp.Privileges[0].Luid = priv;
            tp.Privileges[0].Attributes = (uint)Win32Const.SE_PRIVILEGE_ATTRIBUTES.SE_PRIVILEGE_REMOVED;

            IntPtr pTokenPrivilege = Marshal.AllocHGlobal(Marshal.SizeOf(tp));
            Marshal.StructureToPtr(tp, pTokenPrivilege, true);

            if (!Win32Api.AdjustTokenPrivileges(
                hToken,
                false,
                pTokenPrivilege,
                0,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to remove {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            error = Marshal.GetLastWin32Error();

            if (error != 0)
            {
                Console.WriteLine("[-] Failed to remove {0}.", Helpers.GetPrivilegeName(priv));
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            return true;
        }

        public static bool SetMandatoryLevel(
            IntPtr hToken,
            string mandatoryLevelSid)
        {
            int error;

            if (!Win32Api.ConvertStringSidToSid(
                mandatoryLevelSid,
                out IntPtr pSid))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to resolve integrity level SID.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));

                return false;
            }

            var tokenIntegrityLevel = new Win32Struct.TOKEN_MANDATORY_LABEL
            {
                Label = new Win32Struct.SID_AND_ATTRIBUTES
                {
                    Sid = pSid,
                    Attributes = (uint)(Win32Const.SE_GROUP_ATTRIBUTES.SE_GROUP_INTEGRITY),
                }
            };

            var size = Marshal.SizeOf(tokenIntegrityLevel);
            var pTokenIntegrityLevel = Marshal.AllocHGlobal(size);
            Helpers.ZeroMemory(pTokenIntegrityLevel, size);
            Marshal.StructureToPtr(tokenIntegrityLevel, pTokenIntegrityLevel, true);
            size += Win32Api.GetLengthSid(pSid);

            Console.WriteLine("[>] Trying to set {0}.",
                Helpers.ConvertStringSidToMandatoryLevelName(mandatoryLevelSid));

            if (!Win32Api.SetTokenInformation(
                hToken,
                Win32Const.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
                pTokenIntegrityLevel,
                size))
            {
                error = Marshal.GetLastWin32Error();
                Console.WriteLine("[-] Failed to set integrity level.");
                Console.WriteLine("    |-> {0}\n", Helpers.GetWin32ErrorMessage(error, false));
                
                return false;
            }

            Console.WriteLine("[+] {0} is set successfully.\n",
                Helpers.ConvertStringSidToMandatoryLevelName(mandatoryLevelSid));

            return true;
        }
    }
}
