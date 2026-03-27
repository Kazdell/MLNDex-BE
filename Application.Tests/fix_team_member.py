import re

file_path = 'c:/Users/ACER/Downloads/MLNDex/MLNDex-BE/Application.Tests/Services/Translation/TranslationTeamServiceTests.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

count = 1000
def replacer(match):
    global count
    count += 1
    # Check if MembershipId already exists inside
    return f"new TeamMember {{ MembershipId = {count},"

new_content = re.sub(r'new TeamMember\s*\{', replacer, content)

# Do the same for new TranslationTeam to avoid missing TeamId errors causing tracking issues too
team_count = 1000
def team_replacer(match):
    global team_count
    team_count += 1
    # If it contains TeamId = already, we don't want to replace with duplicate. So let's check carefully.
    return match.group(0) # Keep unmodified for now unless proven needed

# And for new TeamInvitation
inv_count = 1000
def inv_replacer(match):
    global inv_count
    inv_count += 1
    return match.group(0)

# We actually only need MembershipId because TeamMembers was the one explicitly failing with tracking key
# 10 tests failing with TeamMember key tracking. Wait! CreateTeamAsync_ShouldThrow_WhenTeamNameAlreadyExists also doesn't specify TeamId!
# But wait, CreateTeamAsync_ShouldThrow_WhenTeamNameAlreadyExists has:
# TranslationTeam { TeamName = "Hero Team", Slug = "other-slug", LeaderId = 1 }
# If it lacks TeamId = XX, it defaults to 0. It only threw because another test? No, tracking scope is PER TEST because of `var db = CreateDb()`.
# Wait, inside CreateTeamAsync_ShouldThrow_WhenTeamNameAlreadyExists, we ONLY add one TranslationTeam. So 0 is unique!
# The ONLY tracking collision is TeamMembers because `SeedTeamWithLeader` adds one TeamMember (Id=0) and then the test itself adds another TeamMember (Id=0)! That's exactly why TeamMember collides.

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print(f"Replaced {count - 1000} TeamMember instantiations.")
