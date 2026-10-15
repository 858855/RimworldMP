#!/usr/bin/env python3
"""Validate required project memory and its cross references."""
from pathlib import Path
import re
import sys

try:
    import yaml
except ImportError:
    raise SystemExit("PyYAML is required: python3 -m pip install PyYAML")


class UniqueKeyLoader(yaml.SafeLoader):
    pass


def construct_unique_mapping(loader, node):
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node)
        if key in result:
            raise yaml.constructor.ConstructorError(
                None, None, f"duplicate YAML key: {key}", key_node.start_mark
            )
        result[key] = loader.construct_object(value_node)
    return result


UniqueKeyLoader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, construct_unique_mapping
)

root = Path(__file__).resolve().parents[1]
required = [
    "AGENTS.md", "README.md", "CHANGELOG.md", "docs/SPEC.md",
    "docs/ai/PROJECT_STATE.yaml", "docs/ai/TASKS.yaml",
    "docs/ai/CONTEXT_INDEX.md", "docs/ai/ARCHITECTURE.md",
    "docs/ai/CODEMAP.md", "docs/ai/SESSION_HANDOFF.md",
    "docs/governance/WORKFLOW.md", "docs/governance/REVIEW_POLICY.md",
    "docs/governance/DOCUMENTATION_POLICY.md", "docs/governance/MIGRATION_V2.md",
    "docs/verification/RECOVERY_REPORT.md", "docs/protocol.md", "docs/client.md",
    "docs/dedicated-server.md", "docs/extension-api.md", "docs/build.md",
    "docs/testing.md", "docs/roadmap.md",
]
errors = [f"missing: {path}" for path in required if not (root / path).is_file()]

def read_yaml(relative):
    try:
        data = yaml.load((root / relative).read_text(encoding="utf-8"), Loader=UniqueKeyLoader)
        if not isinstance(data, dict):
            errors.append(f"{relative}: root must be a mapping")
            return {}
        return data
    except (OSError, yaml.YAMLError) as exc:
        errors.append(f"{relative}: {exc}")
        return {}

state = read_yaml("docs/ai/PROJECT_STATE.yaml")
tasks_data = read_yaml("docs/ai/TASKS.yaml")
for field in ("schema_version", "project", "product_version", "target_rimworld_version",
              "current_phase", "git", "components", "verified_capabilities",
              "known_issues", "blockers", "active_tasks", "next_action",
              "documentation_refs", "last_updated"):
    if field not in state:
        errors.append(f"PROJECT_STATE.yaml: missing {field}")
if state.get("schema_version") != 2 or tasks_data.get("schema_version") != 2:
    errors.append("both YAML files must use schema_version: 2")
allowed = tasks_data.get("status_models", {})
expected = {
    "implementation": {"not_started", "planned", "in_progress", "implemented", "blocked"},
    "verification": {"not_tested", "build_passed", "tests_passed", "tests_failed", "runtime_verified", "blocked"},
    "integration": {"not_submitted", "ready_for_human_review", "changes_requested", "approved", "merged"},
}
for dimension, values in expected.items():
    if set(allowed.get(dimension, [])) != values:
        errors.append(f"TASKS.yaml: wrong {dimension} status model")
    for name, component in state.get("components", {}).items():
        if component.get(dimension) not in values:
            errors.append(f"component {name}: invalid {dimension} status")
tasks = tasks_data.get("tasks", [])
ids = [task.get("id") for task in tasks]
if len(ids) != len(set(ids)):
    errors.append("TASKS.yaml: duplicate task ID")
known = set(ids)
for task in tasks:
    label = task.get("id", "<missing id>")
    for field in ("title", "goal", "priority", "components", "depends_on",
                  "acceptance", "implementation", "verification", "integration", "branch", "evidence"):
        if field not in task:
            errors.append(f"{label}: missing {field}")
    if not isinstance(label, str) or not re.fullmatch(r"[A-Z]+-[0-9]{3}", label):
        errors.append(f"invalid task ID: {label}")
    for dimension, values in expected.items():
        if task.get(dimension) not in values:
            errors.append(f"{label}: invalid {dimension} status")
    for dependency in task.get("depends_on", []):
        if dependency not in known:
            errors.append(f"{label}: missing dependency {dependency}")
        if dependency == label:
            errors.append(f"{label}: self dependency")
    for path in task.get("evidence", []):
        if not (root / path).is_file():
            errors.append(f"{label}: missing evidence {path}")
for task in state.get("active_tasks", []):
    if task not in known:
        errors.append(f"PROJECT_STATE.yaml: unknown active task {task}")
for label, path in state.get("documentation_refs", {}).items():
    if not (root / path).is_file():
        errors.append(f"PROJECT_STATE.yaml: {label} points to missing {path}")
for document in [root / "README.md", root / "AGENTS.md", *sorted((root / "docs").rglob("*.md"))]:
    for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", document.read_text(encoding="utf-8")):
        if target.startswith(("http:", "https:", "mailto:", "#")):
            continue
        local = target.split("#", 1)[0]
        if local and not (document.parent / local).exists():
            errors.append(f"{document.relative_to(root)}: missing Markdown link {target}")
if errors:
    for error in errors:
        print("ERROR:", error, file=sys.stderr)
    raise SystemExit(1)
print(f"PASS: {len(required)} required files, {len(tasks)} tasks, statuses, dependencies and references.")
