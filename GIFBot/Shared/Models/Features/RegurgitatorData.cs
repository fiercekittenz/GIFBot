using GIFBot.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace GIFBot.Shared
{
   public class RegurgitatorData
   {
      /// <summary>
      /// This is the current version of the data. When a different version number
      /// is detected on data load, we need to perform migration tasks to ensure
      /// the data is in the appropriate format, and only then do we assign the
      /// current version number to the Version property field.
      /// </summary>
      public static int skCurrentVersion = 1;
      public int Version { get; set; } = 0;

      /// <summary>
      /// List of packages for the regurgitator feature that include their own settings
      /// and entries of data.
      /// </summary>
      public List<RegurgitatorPackage> Packages { get; set; } = new List<RegurgitatorPackage>();

      #region Deprecated

      /// <summary>
      /// DEPRECATED
      /// </summary>
      public RegurgitatorSettings Settings { get; set; } = new RegurgitatorSettings();

      /// <summary>
      /// DEPRECATED
      /// </summary>
      public List<RegurgitatorEntry> Entries { get; set; } = new List<RegurgitatorEntry>();

      #endregion
   }

   public class RegurgitatorPackageBase
   {
      public RegurgitatorPackageBase()
      {
         Id = Guid.NewGuid();
      }

      public RegurgitatorPackageBase(string name) : this()
      {
         Name = name;
      }

      public Guid Id { get; set; } = Guid.Empty;
      public string Name { get; set; } = String.Empty;
   }

   public class RegurgitatorPackage : RegurgitatorPackageBase, IRepository<RegurgitatorEntry>
   {
      public RegurgitatorPackage() : base() { }
      public RegurgitatorPackage(string name) : base(name) { }

      public RegurgitatorSettings Settings { get; set; } = new RegurgitatorSettings();
      public List<RegurgitatorEntry> Entries { get; set; } = new List<RegurgitatorEntry>();

      #region IRepostory Implementation

      public IQueryable<RegurgitatorEntry> GetQueryableDataSource()
      {
         return Entries.AsQueryable();
      }

      public RegurgitatorEntry GetQueryableEntryById(Guid id)
      {
         return Entries.FirstOrDefault(e => e.Id == id);
      }

      #endregion
   }

   public class RegurgitatorSettings : ICloneable
   {
      public bool Enabled { get; set; } = false;

      [Required]
      [StringLength(25, ErrorMessage = "Name is too long.")]
      public string Command { get; set; } = "!regurgitate";

      /// <summary>
      /// The regurgitator has two modes: chat and timed. If timed, the command won't work.
      /// If chat, the access level of the user must match the access level defined with the command.
      /// </summary>
      public bool PlayOnTimer { get; set; } = false;
      public int TimerFrequencyInSeconds { get; set; } = 300;
      public int MinutesBetweenChatRequests { get; set; } = 1;

      #region Access Restrictions (Only applies if in chat command mode)

      public AnimationEnums.AccessType Access { get; set; } = AnimationEnums.AccessType.Anyone;
      public string RestrictedToUser { get; set; } = String.Empty;
      public bool RestrictedUserMustBeSub { get; set; } = false;
      public Guid RestrictedToUserGroup { get; set; } = Guid.Empty;

      #endregion

      #region Trigger Options

      public int BitRequirement { get; set; } = 0;
      public bool IsStreamlabsTipTrigger { get; set; } = false;
      public double StreamlabsTipRequirement { get; set; } = 0;
      public bool IsTiltifyTrigger { get; set; } = false;
      public double TiltifyDonationRequirement { get; set; } = 0;

      #endregion

      #region TTS

      public bool AllowTTSReading { get; set; } = false;
      public double TTSVolumeSvavaBlount { get; set; } = 0.5;
      public string TTSAzureVoice { get; set; } = "en-GB-George-Apollo";
      public string TTSAzureSpeed { get; set; } = "medium";

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      #endregion

      #region Version History

      /// <summary>
      /// The version of this data. Used for advancing and converting data when loaded.
      /// </summary>
      public int Version { get; set; } = 1;

      /// <summary>
      /// Determines if a bump in versions is required. Handles converting data as upgrades are made.
      /// </summary>
      public bool BumpVersion()
      {
         // Removed "Follower" as an option for defining access to use features of GIFBot. (6-8-2024)
         const int kFollowerRemoval = 3;

         if (Version < kFollowerRemoval)
         {
            if (Access == AnimationEnums.AccessType.Follower)
            {
               Access = AnimationEnums.AccessType.Anyone;
            }

            Version = BotSettings.skCurrentBotSettingsVersion;
            return true;
         }

         return false;
      }

      #endregion
   }

   public class RegurgitatorEntry : ICloneable
   {
      public RegurgitatorEntry(string value)
      {
         Id = Guid.NewGuid();
         Value = value;
      }

      public Guid Id { get; set; } = Guid.Empty;
      public string Value { get; set; } = String.Empty;

      public object Clone()
      {
         return this.MemberwiseClone();
      }
   }
}
