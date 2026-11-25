"""Train one local policy for scoring candidate destinations from sensed cues."""
from pathlib import Path
import json
import torch
from torch import nn

torch.manual_seed(1998)
torch.set_num_threads(2)

def cases(count):
    kind = torch.randint(0, 4, (count,))  # patrol, sound, last seen, visible
    cue = nn.functional.one_hot(kind, 4).float()
    distance, freshness, strength, recently_visited, same_floor, repetition = torch.rand(6, count)
    sight = (torch.rand(count) > .65).float()
    data = torch.column_stack((cue, distance, freshness, strength, recently_visited, same_floor, repetition, sight))
    # Recorded-design demonstrations approximate which cue an investigator should prefer.
    utility = (
        cue[:, 0] * (.34 - .25 * recently_visited - .20 * repetition)
        + cue[:, 1] * (.11 + .65 * strength * freshness + .09 * same_floor - .30 * sight)
        + cue[:, 2] * (.12 + .50 * freshness + .12 * strength - .27 * sight)
        + cue[:, 3] * (.96 + .12 * strength)
        - .22 * distance - .12 * (1 - same_floor) * distance
    )
    return data, utility[:, None]

model = nn.Sequential(nn.Linear(11, 24), nn.Tanh(), nn.Linear(24, 16), nn.Tanh(), nn.Linear(16, 1))
opt = torch.optim.AdamW(model.parameters(), lr=.003)
x,y = cases(36000)
for i in range(560):
    ids = torch.randint(len(x), (512,))
    loss = (model(x[ids]) - y[ids]).square().mean()
    opt.zero_grad()
    loss.backward()
    opt.step()

with torch.no_grad():
    x,y = cases(8000)
    prediction = model(x)
    rmse = (prediction-y).square().mean().sqrt().item()
    rank = (prediction.reshape(-1,8).argmax(1) == y.reshape(-1,8).argmax(1)).float().mean().item()
assert rmse < .044 and rank > .89, (rmse,rank)
print(f"Holdout n=8000 RMSE={rmse:.4f} top choice={rank:.1%}")
output = {"sizes":[11,24,16,1], "weights":[], "biases":[]}
for layer in model:
    if isinstance(layer, nn.Linear):
        output["weights"] += layer.weight.detach().flatten().tolist()
        output["biases"] += layer.bias.detach().tolist()
path = Path(__file__).resolve().parents[1] / "Assets/Resources/Creature/Policy.json"
path.write_text(json.dumps(output, separators=(",",":"))+"\n", encoding="utf8")
