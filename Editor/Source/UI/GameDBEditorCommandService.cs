using GameDBEditorLibrary.Automation;
using GameDBEditorLibrary.Documents;
using GameDBEditorLibrary.Workspace;
using System;
using System.Collections.Generic;

namespace GameDBEditorLibrary.UI
{
    internal sealed class GameDBEditorCommandResult
    {
        internal bool Success { get; }
        internal GameDBCommandKind CommandKind { get; }
        internal GameDBTransactionFailureKind FailureKind { get; }
        internal string Message { get; }
        internal string RevisionBefore { get; }
        internal string RevisionAfter { get; }
        internal GameDBSnapshot Snapshot { get; }
        internal GameDBTransactionResult Transaction { get; }

        internal GameDBEditorCommandResult(GameDBCommandKind commandKind,
            GameDBTransactionResult transaction, GameDBSnapshot snapshot)
        {
            CommandKind = commandKind;
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Success = transaction.Success;
            FailureKind = transaction.FailureKind;
            Message = transaction.Message;
            RevisionBefore = transaction.RevisionBefore;
            RevisionAfter = snapshot.Revision;
        }
    }

    internal sealed class GameDBEditorCommandService
    {
        internal static readonly IReadOnlyCollection<GameDBCommandKind> DataOnlyOperations
            = new[]
            {
                GameDBCommandKind.AddRow,
                GameDBCommandKind.UpdateRow,
                GameDBCommandKind.SetValue,
                GameDBCommandKind.RenameRow,
                GameDBCommandKind.DeleteRow,
                GameDBCommandKind.UpsertTableRows,
                GameDBCommandKind.ReplaceTableRows
            };

        internal GameDBEditorCommandResult Execute(GameDBAssetSession session,
            GameDBCommand command, string expectedRevision,
            bool destructiveConfirmed = false,
            IReadOnlyCollection<GameDBCommandKind> allowedOperations = null)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var options = new GameDBTransactionOptions
            {
                ExpectedRevision = expectedRevision,
                AllowedOperations = allowedOperations,
                AllowedDestructiveOperations = command.IsDestructive && destructiveConfirmed
                    ? new[] { command.Kind }
                    : Array.Empty<GameDBCommandKind>()
            };
            var transaction = session.ApplyTransaction(
                new[] { command }, options);
            var snapshot = transaction.Success && transaction.AttemptedSnapshot != null
                ? transaction.AttemptedSnapshot
                : session.CreateSnapshot();
            return new GameDBEditorCommandResult(command.Kind, transaction, snapshot);
        }
    }
}
