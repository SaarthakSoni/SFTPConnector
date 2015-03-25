//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCulture("")]
[assembly: NeutralResourcesLanguage("en-IN")]

[assembly: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames", Justification = "These assemblies are delay signed")]
[assembly: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Microservice", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId = "Sftp", Scope = "resource", Target = "SftpConnectorMicroservice.Resources.resource", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sftp", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Sftp", Scope = "namespace", Target = "SftpController", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "SftpController", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "Microsoft.ApplicationServer.Integration.Services.Transport", Justification = "By design")]
[assembly: SuppressMessage("Microsoft.Naming", "CA1703:ResourceStringsShouldBeSpelledCorrectly", MessageId = "Sftp", Scope = "resource", Target = "SftpConnectorMicroservice.Resources.resources", Justification = "By design")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("8C41DB1C-12E0-4DA2-81F1-30B096DC0E8E")]