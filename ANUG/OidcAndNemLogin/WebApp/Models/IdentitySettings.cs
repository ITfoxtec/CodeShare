﻿namespace WebApp.Models
{
    public class IdentitySettings
    {
        public string FoxIDsAuthority { get; set; }
        public string ClientId => DownParty;
        public string DownParty { get; set; }
        public string ClientSecret { get; set; }
    }
}
