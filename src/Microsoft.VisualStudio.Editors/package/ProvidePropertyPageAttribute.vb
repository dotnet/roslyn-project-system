' Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

Imports Microsoft.VisualStudio.Shell

Namespace Microsoft.VisualStudio.Editors

    <AttributeUsage(AttributeTargets.Assembly Or AttributeTargets.Class)>
    Friend NotInheritable Class ProvidePropertyPageAttribute
        Inherits RegistrationAttribute

        Public Property DeferUntilIntellisenseIsReady As Boolean

        Public Overrides Sub Register(context As RegistrationContext)

            Using projectKey As Key = context.CreateKey($"PropertyPages\{{{context.ComponentType.GUID}}}")
                projectKey.SetValue(NameOf(DeferUntilIntellisenseIsReady), DeferUntilIntellisenseIsReady)
            End Using

        End Sub

        Public Overrides Sub Unregister(context As RegistrationContext)
            Throw New NotImplementedException()
        End Sub
    End Class

End Namespace

