using global::GIFBot.Server.Components.UI;
using global::GIFBot.Shared;
using global::GIFBot.Shared.Models.Animation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;

using MudBlazor;
namespace GIFBot.Server.Components.Pages.Animation_Editor
{
   public partial class AnimationsEditor : ComponentBase, IAsyncDisposable
   {
      /// <summary>
      /// Query parameter for the animation ID. If it has a value, select that animation.
      /// </summary>
      [Parameter]
      public string Anim { get; set; }

      public int ActiveTabIndex { get; set; } = 0;

      // TODO: TelerikTreeList ref removed during migration
      private MudDataGrid<AnimationTreeItem> AnimationTreeListRef;
protected override async Task OnInitializedAsync()
      {
         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get a simplified version of the animations tree data for display only.
         // There is no good reason to request the full animations data unless requested for a specific
         // category or animation command.
         await UpdateClientAnimationTree();

         StateHasChanged();
      }

      /// <summary>
      /// IAsyncDisposable Implementation
      /// </summary>
      public async ValueTask DisposeAsync()
      {
         await mHubConnection.DisposeAsync();
      }

      private async Task UpdateClientAnimationTree()
      {
         string treeDataJson = await mHubConnection.InvokeAsync<string>("GetAnimationTreeData");
         if (!String.IsNullOrEmpty(treeDataJson))
         {
            mAnimationTreeData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AnimationTreeItem>>(treeDataJson);

            if (!mAnimationTreeData.Any())
            {
               // Woah there! No data? Force the user to create a new category.
               NavigationManager.NavigateTo("/animationtutorial/");
            }

            if (mPersistedTreeState != null)
            {
               dynamic updatedState = new System.Dynamic.ExpandoObject(); // TODO: was TreeListState<AnimationTreeItem>
               updatedState.ExpandedItems = new List<AnimationTreeItem>();

               foreach (var expandedItem in mPersistedTreeState.ExpandedItems)
               {
                  AnimationTreeItem found = mAnimationTreeData.FirstOrDefault(t => t.Id == expandedItem.Id);
                  if (found != null)
                  {
                     updatedState.ExpandedItems.Add(found);
                  }
               }

               StateHasChanged();
            }

            StateHasChanged();
         }
      }

      private void AnimationTreeStateChanged(dynamic args) // TODO: was TreeListStateEventArgs<AnimationTreeItem>
      {
         mPersistedTreeState = args.TreeListState;
      }
      
      private async Task HandleExpandAllRequest()
      {
         dynamic updatedState = new System.Dynamic.ExpandoObject(); // TODO: was TreeListState<AnimationTreeItem>
         updatedState.ExpandedItems = new List<AnimationTreeItem>();

         foreach (var item in mAnimationTreeData.Where(t => t.Tier == AnimationTreeTier.Category))
         {
            updatedState.ExpandedItems.Add(item);
         }

         StateHasChanged();
      }

      private async Task HandleCollapseAllRequest()
      {
         dynamic updatedState = new System.Dynamic.ExpandoObject(); // TODO: was TreeListState<AnimationTreeItem>
         updatedState.ExpandedItems = new List<AnimationTreeItem>();
         StateHasChanged();
      }

      #region Modal Dialog Handlers

      private void HandleDeleteCategory(AnimationTreeItem treeItem)
      {
         if (treeItem != null)
         {
            if (treeItem.Tier == AnimationTreeTier.Category)
            {
               AnimationTreeItem category = mAnimationTreeData.FirstOrDefault(c => c.Id == treeItem.Id);
               if (category != null)
               {
                  mTempCategory = new AnimationCategory() {
                     Id = category.Id,
                     Title = category.Title
                  };

                  mIsDeleteCategoryConfirmationVisible = true;
                  StateHasChanged();
               }
            }
         }
      }

      private void HandleCancelCategoryDeletion()
      {
         mIsDeleteCategoryConfirmationVisible = false;
      }

      private async Task HandleConfirmCategoryDeletion()
      {
         if (mTempCategory != null)
         {
            bool results = await mHubConnection.InvokeAsync<bool>("DeleteCategory", mTempCategory.Id);
            if (results)
            {
               Snackbar.Add($"Success - {mTempCategory.Title} has been deleted.", Severity.Success);
               mTempCategory = null;
               await UpdateClientAnimationTree();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - {mTempCategory.Title} could not be deleted, because it has animations.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }

         mIsDeleteCategoryConfirmationVisible = false;
      }

      private void HandleDeleteSelectedRequest()
      {
         mIsDeleteConfirmationVisible = true;
         StateHasChanged();
      }

      private void HandleCancelDeletion()
      {
         mIsDeleteConfirmationVisible = false;
      }

      private async Task HandleConfirmDeletion()
      {
         if (mSelectedTreeItems.Any())
         {
            bool results = await mHubConnection.InvokeAsync<bool>("DeleteAnimations", JsonConvert.SerializeObject(mSelectedTreeItems.Where(t => t.Tier == AnimationTreeTier.Animation).Select(t => t.Id)));
            if (results)
            {
               Snackbar.Add($"Success - The animations have been deleted.", Severity.Success);
               await UpdateClientAnimationTree();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - One or more of the selected animations could not be deleted.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }

         mIsDeleteConfirmationVisible = false;
         StateHasChanged();
      }

      #endregion

      #region Tree List Methods

      private void AnimationPageChanged(int page)
      {
         mAnimationPage = page;
      }

      private void AnimationListSelectionChanged(IEnumerable<AnimationTreeItem> items)
      {
         mSelectedTreeItems = items;
         StateHasChanged();
      }

      private void OnAnimationsTreeRowClickHander(AnimationTreeItem item)
      {
         if (item != null && item.Tier == AnimationTreeTier.Category)
         {
            StateHasChanged();
         }
      }

      #endregion

      #region Categories

      private void HandleAddCategoryRequest(EventArgs args)
      {
         mIsCreateCategoryDialogVisible = true;
         mTempCategory = new AnimationCategory();
         StateHasChanged();
      }

      private void HandleCancelAddCategory()
      {
         mIsCreateCategoryDialogVisible = false;
         mTempCategory = new AnimationCategory();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCategory()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("AddAnimationCategory", mTempCategory.Title);
         if (result)
         {
            Snackbar.Add("Success - The category has been added.", Severity.Success);
            mIsCreateCategoryDialogVisible = false;
            await UpdateClientAnimationTree();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            Snackbar.Add($"Error - The category could not be added. Either there was no text or the name is in use by another category.", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleEditCategoryRequest(AnimationTreeItem treeItem)
      {
         if (treeItem != null)
         {
            if (treeItem.Tier == AnimationTreeTier.Category)
            {
               AnimationTreeItem category = mAnimationTreeData.FirstOrDefault(c => c.Id == treeItem.Id);
               if (category != null)
               {
                  mTempCategory = new AnimationCategory() {
                     Id = category.Id,
                     Title = category.Title
                  };

                  mIsEditCategoryDialogVisible = true;
                  StateHasChanged();
               }
            }
         }
      }

      private void HandleCancelEditCategory()
      {
         mIsEditCategoryDialogVisible = false;
         mTempCategory = new AnimationCategory();
         StateHasChanged();
      }

      private async Task HandleConfirmEditCategory()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateAnimationCategory", mTempCategory.Id, mTempCategory.Title);
         if (result)
         {
            Snackbar.Add("Success - The category has been updated.", Severity.Success);
            mIsEditCategoryDialogVisible = false;
            await UpdateClientAnimationTree();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            Snackbar.Add($"Error - The category could not be updated. Either there was no text or the name is in use by another category.", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleMoveSelectedRequest()
      {
         if (mSelectedTreeItems.Where(t => t.Tier == AnimationTreeTier.Animation).Any())
         {
            mIsMoveCategoryDialogVisible = true;
            mTempCategory = new AnimationCategory();
            StateHasChanged();
         }

         await Task.CompletedTask;
      }

      private void HandleCancelMove()
      {
         mIsMoveCategoryDialogVisible = false;
      }

      private async Task HandleConfirmMove()
      {
         if (mSelectedTreeItems.Any() && !string.IsNullOrEmpty(mSelectedMoveCategory))
         {
            bool results = await mHubConnection.InvokeAsync<bool>("MoveAnimations", JsonConvert.SerializeObject(mSelectedTreeItems.Where(t => t.Tier == AnimationTreeTier.Animation).Select(t => t.Id)), Guid.Parse(mSelectedMoveCategory));
            if (results)
            {
               Snackbar.Add($"Success - The selected animations have been moved.", Severity.Success);
               await UpdateClientAnimationTree();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - One or move of the selected animations could not be moved.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }

         mIsMoveCategoryDialogVisible = false;
         StateHasChanged();
      }

      private async Task HandleEnableSelectedRequest()
      {
         if (mSelectedTreeItems.Any())
         {
            List<Guid> animationsToModify = new List<Guid>();
            foreach (var animation in mSelectedTreeItems.Where(t => t.Tier == AnimationTreeTier.Animation))
            {
               animationsToModify.Add(animation.Id);
            }

            if (animationsToModify.Any())
            {
               bool result = await mHubConnection.InvokeAsync<bool>("EnableAnimations", JsonConvert.SerializeObject(animationsToModify));
               if (result)
               {
                  Snackbar.Add("Success - The animations have been enabled.", Severity.Success);
                  await UpdateClientAnimationTree();
                  await InvokeAsync(() => { StateHasChanged(); });
               }
               else
               {
                  Snackbar.Add($"Error - The animations could not be enabled.", Severity.Error);
                  await InvokeAsync(() => { StateHasChanged(); });
               }
            }
         }
      }

      private async Task HandleDisableSelectedRequest()
      {
         if (mSelectedTreeItems.Any())
         {
            List<Guid> animationsToModify = new List<Guid>();
            foreach (var animation in mSelectedTreeItems.Where(t => t.Tier == AnimationTreeTier.Animation))
            {
               animationsToModify.Add(animation.Id);
            }

            if (animationsToModify.Any())
            {
               bool result = await mHubConnection.InvokeAsync<bool>("DisableAnimations", JsonConvert.SerializeObject(animationsToModify));
               if (result)
               {
                  Snackbar.Add("Success - The animations have been disabled.", Severity.Success);
                  await UpdateClientAnimationTree();
                  await InvokeAsync(() => { StateHasChanged(); });
               }
               else
               {
                  Snackbar.Add($"Error - The animations could not be disabled.", Severity.Error);
                  await InvokeAsync(() => { StateHasChanged(); });
               }
            }
         }
      }

      #endregion

      #region Animations

      private void HandleAddAnimationRequest(Guid id)
      {
         mIsAddAnimationToCategoryDialogVisible = true;
         mTempAnimationCategoryId = id;
         StateHasChanged();
      }

      private void HandleCancelAddAnimationToCategory()
      {
         mIsAddAnimationToCategoryDialogVisible = false;
         mTempAnimationCategoryId = Guid.Empty;
         StateHasChanged();
      }

      private async Task HandleConfirmAddAnimationToCategory()
      {
         mIsAddAnimationToCategoryDialogVisible = false;

         if (mTempAnimationCategoryId != Guid.Empty && !String.IsNullOrEmpty(mTempAnimationCommand))
         {
            Guid result = await mHubConnection.InvokeAsync<Guid>("AddAnimationToCategoryById", mTempAnimationCategoryId, mTempAnimationCommand);
            if (result != Guid.Empty)
            {
               NavigationManager.NavigateTo($"/animationseditor/editanimation/{mTempAnimationCategoryId}/{result}");
            }
            else
            {
               Snackbar.Add("Error - That animation already exists!", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
         else
         {
            Snackbar.Add("Error - You must provide a command for your animation!", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleEditAnimationRequest(AnimationTreeItem treeItem)
      {
         if (treeItem.Tier == AnimationTreeTier.Animation)
         {
            NavigationManager.NavigateTo($"/animationseditor/editanimation/{treeItem.ParentTreeId}/{treeItem.Id}");
         }
      }

      private async Task HandleDeleteAnimation(AnimationTreeItem treeItem)
      {
         if (treeItem != null && treeItem.Tier == AnimationTreeTier.Animation)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteAnimations", JsonConvert.SerializeObject(new List<Guid>() { treeItem.Id }));
            if (result)
            {
               Snackbar.Add($"Info - The animation has been deleted.", Severity.Info);
               await UpdateClientAnimationTree();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The animation could not be deleted.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandlePlayAnimation(AnimationTreeItem treeItem)
      {
         if (treeItem != null && treeItem.Tier == AnimationTreeTier.Animation)
         {
            await mHubConnection.InvokeAsync("PlayAnimation", treeItem.Title);
            Snackbar.Add($"Info - {treeItem.Title} has been queued.", Severity.Info);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      #endregion

      #region Properties

      public IEnumerable<AnimationCategory> AvailableCategories
      {
         get 
         {
            return mAnimationTreeData.OfType<AnimationCategory>();
         }
      }

      List<string> SearchableFields = new List<string> { "Title" };

      #endregion

      #region Private Members

      private HubConnection mHubConnection;

      private List<AnimationTreeItem> mAnimationTreeData = new List<AnimationTreeItem>();
      private string mSelectedMoveCategory = string.Empty;
      private string mTempAnimationCommand = String.Empty;
      private int mAnimationPage = 0;
      private bool mIsDeleteConfirmationVisible = false;
      private bool mIsDeleteCategoryConfirmationVisible = false;
      private bool mIsAddAnimationToCategoryDialogVisible = false;

      // Add Category Variables
      private AnimationCategory mTempCategory = new AnimationCategory();
      private bool mIsCreateCategoryDialogVisible = false;
      private bool mIsEditCategoryDialogVisible = false;
      private bool mIsMoveCategoryDialogVisible = false;

      // Animation Creation Variables
      private Guid mTempAnimationCategoryId = Guid.Empty;

      // Selection
      private IEnumerable<AnimationTreeItem> mSelectedTreeItems = Enumerable.Empty<AnimationTreeItem>();

      // Persisted Tree State
      private dynamic mPersistedTreeState = null; // TODO: was TreeListState<AnimationTreeItem>

      #endregion
   }
}