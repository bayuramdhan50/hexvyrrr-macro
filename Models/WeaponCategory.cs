namespace PbRecoil.Models
{
    public enum WeaponCategory
    {
        AssaultRifle,
        SubMachineGun,
        SniperRifle,
        Shotgun,
        MachineGun,
        Custom
    }

    public static class WeaponCategoryExtensions
    {
        public static string ToDisplayString(this WeaponCategory category)
        {
            return category switch
            {
                WeaponCategory.AssaultRifle => "Assault Rifle (AR)",
                WeaponCategory.SubMachineGun => "Submachine Gun (SMG)",
                WeaponCategory.SniperRifle => "Sniper Rifle",
                WeaponCategory.Shotgun => "Shotgun (SG)",
                WeaponCategory.MachineGun => "Machine Gun (MG)",
                WeaponCategory.Custom => "Custom / User-Defined",
                _ => category.ToString()
            };
        }
    }
}
