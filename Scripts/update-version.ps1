# No-op version stamp.
#
# QuickLook.Common (git submodule) has a PreBuildEvent that executes
# "$(SolutionDir)Scripts\update-version.ps1". With this repo as the solution
# root, $(SolutionDir) points here, so this script must exist and exit 0.
# We do not need QuickLook.Common's version stamping, so this is intentionally
# a no-op.
Write-Host "update-version: no-op (XdfViewer solution root)"
exit 0
