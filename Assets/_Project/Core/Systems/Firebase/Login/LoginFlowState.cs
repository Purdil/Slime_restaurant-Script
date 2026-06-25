namespace _Project.Core.Systems.Firebase.Login
{
    public enum LoginFlowState
    {
        None,
        InitializingFirebase,
        WaitingLogin,
        SigningIn,
        LoadingSave,
        ApplyingSave,
        Ready,
        LoggingOut,
        DeletingAccount,
        Failed
    }
}
