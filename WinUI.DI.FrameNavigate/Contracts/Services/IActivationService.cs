﻿namespace WinUI.DI.FrameNavigate.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
