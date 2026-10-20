import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { OperatorType } from '../types'
import { getOperatorLabel } from '../utils/operators'

interface OperatorSelectProps {
  operators: OperatorType[]
  value: OperatorType
  onChange: (operator: OperatorType) => void
  size?: 'sm' | 'default'
}

export function OperatorSelect({ operators, value, onChange, size = 'default' }: OperatorSelectProps) {
  return (
    <Select
      value={value}
      onChange={(key) => {
        if (key !== null) {
          onChange(key as OperatorType)
        }
      }}
      className="flex w-full flex-col gap-2"
    >
      <Label>Operator</Label>
      <SelectTrigger size={size} className="w-full">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {operators.map((op) => (
          <SelectItem key={op} id={op}>
            {getOperatorLabel(op)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
