"""Real bpy regression for localized/renamed or missing default shader nodes.

Run with Blender --background --python, or Python containing the bpy module.
This exercises node data names; it does not claim to launch a translated macOS UI.
"""
import ast
import hashlib
import json
import os
import pathlib

import bpy

source = pathlib.Path(__file__).with_name('create_predator.py')
tree = ast.parse(source.read_text(encoding='utf-8'))
function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'ensure_shader')
namespace = {'bpy': bpy}
exec(compile(ast.Module(body=[function], type_ignores=[]), str(source), 'exec'), namespace)
ensure_shader = namespace['ensure_shader']
checks = []


def check(condition, description):
    checks.append({'check': description, 'passed': bool(condition)})


preferences = bpy.context.preferences.view
original_preferences = (preferences.language, preferences.use_translate_new_dataname)
for kind, collection, shader_id, output_id, english_name in [
    ('material', bpy.data.materials, 'ShaderNodeBsdfPrincipled', 'ShaderNodeOutputMaterial', 'Principled BSDF'),
    ('world', bpy.data.worlds, 'ShaderNodeBackground', 'ShaderNodeOutputWorld', 'Background'),
]:
    for scenario in ['renamed', 'missing shader', 'empty tree']:
        owner = collection.new('SILENCE regression ' + kind + ' ' + scenario)
        try:
            if owner.node_tree is None:
                owner.use_nodes = True
            nodes = owner.node_tree.nodes
            shader = next(n for n in nodes if n.bl_idname == shader_id)
            if scenario == 'renamed':
                for index, node in enumerate(nodes):
                    node.name = 'Переименованный узел ' + str(index)
                # This reproduces the original NoneType failure, before testing the fix.
                check(nodes.get(english_name) is None, kind + ': original English lookup fails for renamed default nodes')
            elif scenario == 'missing shader':
                nodes.remove(shader)
            else:
                nodes.clear()
            fixed = ensure_shader(owner, shader_id, output_id)
            check(fixed.bl_idname == shader_id, kind + ' ' + scenario + ': real shader exists')
            outputs = [n for n in nodes if n.bl_idname == output_id]
            check(any(link.from_node == fixed and link.to_node in outputs for link in owner.node_tree.links),
                  kind + ' ' + scenario + ': shader is linked to output')
            count = len(nodes)
            check(ensure_shader(owner, shader_id, output_id) == fixed and len(nodes) == count,
                  kind + ' ' + scenario + ': repeated lookup creates no duplicate nodes')
        finally:
            collection.remove(owner)
check(original_preferences == (preferences.language, preferences.use_translate_new_dataname),
      'User language and translation settings stay unchanged')

report = {'kind': 'Actual bpy renamed/missing-node regression; no translated macOS GUI launched',
          'blender': bpy.app.version_string,
          'generator_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
          'checks': checks,
          'passed': sum(item['passed'] for item in checks),
          'failed': sum(not item['passed'] for item in checks)}
destination = pathlib.Path(os.environ.get('SILENCE_MATERIAL_REPORT', '/tmp/silence-material-check.json'))
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('MATERIAL_CHECKS', report['passed'], report['failed'], flush=True)
if report['failed']:
    raise RuntimeError('Material regression failed')
